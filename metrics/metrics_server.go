package metrics

import (
	"bufio"
	"encoding/json"
	"fmt"
	"log"
	"net/http"
	"os"
	"sync"
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
	saveToFile     bool
	headerFilePath string
	dataFilePath   string
}

// For all producers, maybe we pass producer type when connecting?
//
//	 		Need to hold several collection for easy access
//				* Per ProducerType (SFU, Client etc...)
//				* Per Metric (which producers are currently producing this type of metric), hold uint as key, use lookup whenever request comes in
type MetricsServer struct {
	metricCounter        uint
	producers            map[uint]*MetricProducerConnection
	definitions          map[uint]*MetricDefinition
	definitionStringToId map[string]uint
	producersForMetric   map[uint]map[uint]*MetricProducerConnection
	producersPerType     map[string]map[uint]*MetricProducerConnection

	mut sync.Mutex

	saveToFile   bool
	headerFile   *os.File
	dataFile     *os.File
	headerWriter *bufio.Writer
	dataWriter   *bufio.Writer
}

func NewMetricsServer(configPath string) *MetricsServer {
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
	if config.saveToFile {
		headerFile, err := os.OpenFile(config.headerFilePath, os.O_CREATE|os.O_WRONLY|os.O_APPEND, 0666)
		if err != nil {
			panic(err)
		}
		dataFile, err := os.OpenFile(config.dataFilePath, os.O_CREATE|os.O_WRONLY|os.O_APPEND, 0666)
		if err != nil {
			panic(err)
		}
		headerWriter = bufio.NewWriter(headerFile)
		dataWriter = bufio.NewWriter(dataFile)
	}

	s := &MetricsServer{
		metricCounter:        0,
		producers:            map[uint]*MetricProducerConnection{},
		definitions:          map[uint]*MetricDefinition{},
		definitionStringToId: map[string]uint{},
		producersForMetric:   map[uint]map[uint]*MetricProducerConnection{},
		producersPerType:     map[string]map[uint]*MetricProducerConnection{},
		mut:                  sync.Mutex{},
		saveToFile:           config.saveToFile,
		headerFile:           headerFile,
		dataFile:             dataFile,
		headerWriter:         headerWriter,
		dataWriter:           dataWriter,
	}
	http.HandleFunc("/connect", s.handleConnect)
	http.HandleFunc("/metric/register/composite", s.handleAddCompositeMetric)
	http.HandleFunc("/metric/register/generic", s.handleAddGenericMetric)
	return s
}

func (s *MetricsServer) StartListening(port uint) {
	defer s.cleanup()
	if err := http.ListenAndServe(fmt.Sprintf(":%d", port), nil); err != nil {
		fmt.Printf("Error starting server: %s\n", err)
	}
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
	newProducer := NewMetricProducerConnection(s, s.metricCounter, producerType)
	s.producers[s.metricCounter] = newProducer
	if _, ok := s.producersPerType[producerType]; !ok {
		s.producersPerType[producerType] = make(map[uint]*MetricProducerConnection)
	}
	s.producersPerType[producerType][s.metricCounter] = newProducer
	s.metricCounter++
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

func (s *MetricsServer) handleAddGenericMetric(w http.ResponseWriter, r *http.Request) {

}

func (s *MetricsServer) handleAddCompositeMetric(w http.ResponseWriter, r *http.Request) {

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

func (s *MetricsServer) cleanup() {
	s.headerWriter.Flush()
	s.dataWriter.Flush()

	s.headerFile.Close()
	s.dataFile.Close()
}
