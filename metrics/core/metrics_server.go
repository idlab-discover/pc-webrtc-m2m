package core

import (
	"bufio"
	"bytes"
	"encoding/binary"
	"encoding/json"
	"fmt"
	"log"
	"metrics/core/logger"
	"metrics/core/readers"
	"net/http"
	"os"
	"os/signal"
	"path/filepath"
	"sync"
	"syscall"
	"time"
)

/*
*     Config per metric
*		* Save only latest X per client
*       * Strip timestamps
*     General config
*       * Header file output
*       * Binary output
*
 */

// ClientType -> client, SFU, SessionManager etc...
const NameMetricsServer = "MetricsServer"

type MetricsServerConfig struct {
	GeneralConfig GeneralMetricServerConfig `json:"generalConfig"`
}

type GeneralMetricServerConfig struct {
	AddTimestampDirectory bool   `json:"addTimestampDirectory"`
	SaveToFile            bool   `json:"saveToFile"`
	HeaderFilePath        string `json:"headerPath"`
	DataFilePath          string `json:"dataPath"`
	ReaderConnectionType  string `json:"readerConnectionType"`
}

// For all producers, maybe we pass producer type when connecting?
//
//	 		Need to hold several collection for easy access
//				* Per ProducerType (SFU, Client etc...)
//				* Per Metric (which producers are currently producing this type of metric), hold uint as key, use lookup whenever request comes in
type MetricsServer struct {
	producerCounter      uint32
	metricCounter        uint32
	producers            map[uint32]*MetricProducerConnection
	definitions          map[uint32]*MetricDefinition
	definitionStringToId map[string]uint32
	producersForMetric   map[uint32]map[uint32]*MetricProducerConnection
	producersPerType     map[string]map[uint32]*MetricProducerConnection
	readerFactory        *readers.MetricReaderFactory
	readerConnectionType string /*We might want to be more finegrained here later, i.e., different readers for different metrics*/
	mut                  sync.Mutex

	saveToFile   bool
	headerFile   *os.File
	dataFile     *os.File
	headerWriter *bufio.Writer
	dataWriter   *bufio.Writer
}

func NewMetricsServer(configPath string) *MetricsServer {
	logger.Log(NameMetricsServer, logger.Creating, true, true)
	var config MetricsServerConfig
	file, err := os.Open(configPath)
	if err != nil {
		log.Fatalf("Failed to open config file: %v", err)
	}
	defer file.Close()
	decoder := json.NewDecoder(file)
	if err := decoder.Decode(&config); err != nil {
		log.Fatalf("Failed to decode config file: %v", err)
	}

	var headerFile *os.File
	var dataFile *os.File
	var headerWriter *bufio.Writer
	var dataWriter *bufio.Writer
	if config.GeneralConfig.SaveToFile {
		if config.GeneralConfig.AddTimestampDirectory {
			timestamp := time.Now().Format("2006-01-02_15-04-05")
			config.GeneralConfig.HeaderFilePath = filepath.Join(config.GeneralConfig.HeaderFilePath, "/"+timestamp)
			config.GeneralConfig.DataFilePath = filepath.Join(config.GeneralConfig.DataFilePath, "/"+timestamp)
			err := os.MkdirAll(config.GeneralConfig.HeaderFilePath, os.ModePerm)
			if err != nil {
				fmt.Printf("Failed to create header directory: %v at %s \n", err, config.GeneralConfig.HeaderFilePath)
				panic(err)
			}
			err = os.MkdirAll(config.GeneralConfig.DataFilePath, os.ModePerm)
			if err != nil {
				fmt.Printf("Failed to create data directory: %v at %s \n", err, config.GeneralConfig.DataFilePath)
				panic(err)
			}
		}
		config.GeneralConfig.HeaderFilePath = filepath.Join(config.GeneralConfig.HeaderFilePath, "metric_headers.bin")
		config.GeneralConfig.DataFilePath = filepath.Join(config.GeneralConfig.DataFilePath, "metric_output.bin")
		headerFile, err := os.OpenFile(config.GeneralConfig.HeaderFilePath, os.O_CREATE|os.O_WRONLY|os.O_APPEND|os.O_TRUNC, 0666)
		if err != nil {
			fmt.Printf("Failed to open header file: %v at %s \n", err, config.GeneralConfig.HeaderFilePath)
			panic(err)
		}
		dataFile, err := os.OpenFile(config.GeneralConfig.DataFilePath, os.O_CREATE|os.O_WRONLY|os.O_APPEND|os.O_TRUNC, 0666)
		if err != nil {
			fmt.Printf("Failed to open data file: %v at %s \n", err, config.GeneralConfig.DataFilePath)
			panic(err)
		}
		headerWriter = bufio.NewWriter(headerFile)
		dataWriter = bufio.NewWriter(dataFile)
	}

	s := &MetricsServer{
		producerCounter:      0,
		metricCounter:        0,
		producers:            map[uint32]*MetricProducerConnection{},
		definitions:          map[uint32]*MetricDefinition{},
		definitionStringToId: map[string]uint32{},
		producersForMetric:   map[uint32]map[uint32]*MetricProducerConnection{},
		producersPerType:     map[string]map[uint32]*MetricProducerConnection{},
		readerFactory:        readers.NewMetricReaderFactory(),
		readerConnectionType: config.GeneralConfig.ReaderConnectionType,
		mut:                  sync.Mutex{},
		saveToFile:           config.GeneralConfig.SaveToFile,
		headerFile:           headerFile,
		dataFile:             dataFile,
		headerWriter:         headerWriter,
		dataWriter:           dataWriter,
	}
	http.HandleFunc("/connect", s.handleConnect)
	http.HandleFunc("/metric/register/composite", s.handleAddCompositeMetric)
	http.HandleFunc("/metric/register/generic", s.handleAddGenericMetric)
	logger.Log(NameMetricsServer, logger.Created, true, true)
	return s
}

func (s *MetricsServer) StartListening(port uint) {
	defer s.cleanup()
	if err := http.ListenAndServe(fmt.Sprintf(":%d", port), nil); err != nil {
		fmt.Printf("Error starting server: %s\n", err)
	}
}

type MetricServerConnectionResponse struct {
	MetricClientId         uint32 `json:"metricClientId"`
	ReaderConnectionType   string `json:"readerConnectionType"`
	ReaderConnectionString string `json:"readerConnectionString"`
}

func (s *MetricsServer) handleConnect(w http.ResponseWriter, r *http.Request) {
	s.mut.Lock()
	defer s.mut.Unlock()
	producerType := r.URL.Query().Get("producerType")
	if producerType == "" {
		fmt.Println("WebRTCSFU: webSocketHandler: No producerType provided, returning 400")
		http.Error(w, "No producerType provided", http.StatusBadRequest)
		return
	}
	newProducer := s.addProducer(producerType, s.readerConnectionType, s.readerFactory)
	// TODO: This should return metricCounter + connection address of the reader
	w.Header().Set("Content-Type", "application/json")
	response := MetricServerConnectionResponse{
		MetricClientId:         newProducer.Id,
		ReaderConnectionType:   s.readerConnectionType,
		ReaderConnectionString: newProducer.reader.GetConnectionAddress(),
	}
	err := json.NewEncoder(w).Encode(response)
	if err != nil {
		http.Error(w, "Failed to marshal response", http.StatusInternalServerError)
		return
	}
}

func (s *MetricsServer) handleDisconnect(w http.ResponseWriter, r *http.Request) {

}

func (s *MetricsServer) handleDisconnectFromReader(producer *MetricProducerConnection) {

}

func (s *MetricsServer) handleDisconnectInternal(producer *MetricProducerConnection) {
	s.mut.Lock()
	defer s.mut.Unlock()
	delete(s.producers, producer.Id)
	delete(s.producersPerType[producer.ProducerType], producer.Id)
	for metricId := range producer.Metrics {
		delete(s.producersForMetric[metricId], producer.Id)
	}

}

type AddMetricRequest struct {
	MetricClientId uint32 `json:"metricClientId"`
	MetricName     string `json:"metricName"`
	Header         []byte `json:"header"`
}

type AddMetricResponse struct {
	MetricId uint32 `json:"metricId"`
}

func (s *MetricsServer) addMetricDefinition(w http.ResponseWriter, r *http.Request, isComposite bool) {
	var req AddMetricRequest
	err := json.NewDecoder(r.Body).Decode(&req)
	if err != nil {
		http.Error(w, "Failed to decode request body", http.StatusBadRequest)
		return
	}
	producer, ok := s.producers[req.MetricClientId]
	if !ok {
		http.Error(w, "No producer found for given MetricClientId", http.StatusBadRequest)
		return
	}
	metricId := s.addMetricDefinitionInternal(producer, req, isComposite)
	w.Header().Set("Content-Type", "application/json")
	response := AddMetricResponse{
		MetricId: metricId,
	}
	err = json.NewEncoder(w).Encode(response)
	if err != nil {
		http.Error(w, "Failed to marshal response", http.StatusInternalServerError)
		return
	}
}

func (s *MetricsServer) AddMetricDefinitionLocal(producer *MetricProducerConnection, metricName string, header []byte, isComposite bool) uint32 {
	return s.addMetricDefinitionInternal(producer, AddMetricRequest{
		MetricClientId: producer.Id,
		MetricName:     metricName,
		Header:         header,
	}, isComposite)
}

func (s *MetricsServer) addMetricDefinitionInternal(producer *MetricProducerConnection, req AddMetricRequest, isComposite bool) uint32 {
	var definition *MetricDefinition
	definitionId, ok := s.definitionStringToId[req.MetricName]
	if ok {
		definition, ok = s.definitions[definitionId]
	} else {
		definitionId = s.metricCounter
		s.definitionStringToId[req.MetricName] = definitionId
		s.metricCounter++
		definition = NewMetricDefinition(definitionId, req.MetricName, 5, isComposite, req.Header, true, true) // TODO: MaxValues should be provided by the client
		s.definitions[definitionId] = definition
		s.producersForMetric[definition.MetricId] = make(map[uint32]*MetricProducerConnection)
		s.writeHeaderToFile(req.MetricName, definition.MetricId, req.Header)
	}
	producer.AddMetric(definition.MetricId, definition)
	s.producersForMetric[definition.MetricId][producer.Id] = producer
	return definition.MetricId
}

func (s *MetricsServer) handleAddGenericMetric(w http.ResponseWriter, r *http.Request) {
	s.mut.Lock()
	defer s.mut.Unlock()
	s.addMetricDefinition(w, r, false)
}

func (s *MetricsServer) handleAddCompositeMetric(w http.ResponseWriter, r *http.Request) {
	s.mut.Lock()
	defer s.mut.Unlock()
	s.addMetricDefinition(w, r, true)
}

func (s *MetricsServer) addProducer(producerType string, readerType string, readerFactory *readers.MetricReaderFactory) *MetricProducerConnection {
	newProducer := NewMetricProducerConnection(s, s.producerCounter, producerType, readerType, readerFactory)
	s.producers[s.producerCounter] = newProducer
	s.producerCounter++
	if _, ok := s.producersPerType[producerType]; !ok {
		s.producersPerType[producerType] = make(map[uint32]*MetricProducerConnection)
	}
	s.producersPerType[producerType][newProducer.Id] = newProducer
	return newProducer
}

// -------------------- Local functions -------------------
func (s *MetricsServer) AddLocalProducer(producerType string) *MetricProducerConnection {
	s.mut.Lock()
	defer s.mut.Unlock()
	return s.addProducer(producerType, "", nil) // We can just add measurements with the producer ptr
}

func (s *MetricsServer) AddLocalMetric(producer *MetricProducerConnection, metricName string, header []byte, isComposite bool) uint32 {
	s.mut.Lock()
	defer s.mut.Unlock()
	// var definition *MetricDefinition
	// definitionId, ok := s.definitionStringToId[metricName]
	return s.metricCounter // TODO: Handle case where metric with the same name already exists, maybe we can just return the existing definition and let producer add to it?
}

func (s *MetricsServer) writeHeaderToFile(metricName string, metricId uint32, header []byte) {
	if s.saveToFile {
		buf := new(bytes.Buffer)

		binary.Write(buf, binary.LittleEndian, uint32(len(metricName)))
		buf.WriteString(metricName)
		binary.Write(buf, binary.LittleEndian, uint32(metricId))
		binary.Write(buf, binary.LittleEndian, uint32(len(header)))
		buf.Write(header)

		fullHeader := buf.Bytes()
		n, err := s.headerWriter.Write(fullHeader)
		if err != nil {
			log.Printf("Failed to write data: %v", err)
		}
		if n != len(fullHeader) {
			log.Printf("Partial write: wrote %d of %d bytes", n, len(fullHeader))
		}
	}
}

func (s *MetricsServer) OnDataReceived(producer *MetricProducerConnection, buffer []byte) {
	s.mut.Lock()
	defer s.mut.Unlock()
	// TODO Do we need to save the raw data without clientID?
	if s.saveToFile {
		n, err := s.dataWriter.Write(buffer)
		if err != nil {
			log.Printf("Failed to write data: %v", err)
		}
		if n != len(buffer) {
			log.Printf("Partial write: wrote %d of %d bytes", n, len(buffer))
		}
	}

}

func (s *MetricsServer) ListenForSigClose() {
	sigs := make(chan os.Signal, 1)

	// Register the signals we want to intercept
	// SIGINT = Ctrl+C, SIGTERM = Generic termination signal
	signal.Notify(sigs, syscall.SIGINT, syscall.SIGTERM)

	fmt.Println("Application is running. Press Ctrl+C to exit.")

	// This blocks until a signal is received
	sig := <-sigs
	fmt.Printf("\nReceived signal: %s. Shutting down...\n", sig)
	s.cleanup()
}

func (s *MetricsServer) cleanup() {
	s.headerWriter.Flush()
	s.dataWriter.Flush()

	s.headerWriter.Flush()
	s.dataWriter.Flush()

	s.headerFile.Close()
	s.dataFile.Close()
}
