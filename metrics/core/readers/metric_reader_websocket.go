package readers

import (
	"fmt"
	"metrics/core/logger"
	"net/http"
	"sync"

	"github.com/gorilla/websocket"
)

const NameMetricReaderWebSocketSubFactory = "MetricReaderWebSocketSubFactory"
const NameMetricReaderWebSocket = "MetricReaderWebSocket"

var (
	upgrader = websocket.Upgrader{
		CheckOrigin: func(r *http.Request) bool { return true },
	}
)

type ThreadSafeWebsocket struct {
	*websocket.Conn
	sync.Mutex
}

type MetricReaderWebSocket struct {
	onDataReceived func([]byte)

	ws *ThreadSafeWebsocket
}

type MetricReaderWebSocketSubFactory struct {
	mut                   sync.Mutex
	incompleteConnections map[uint]*MetricReaderWebSocket
}

func NewMetricReaderWebSocketSubFactory() *MetricReaderWebSocketSubFactory {
	logger.Log(NameMetricReaderWebSocketSubFactory, logger.Creating, true, true)
	ms := &MetricReaderWebSocketSubFactory{
		mut:                   sync.Mutex{},
		incompleteConnections: make(map[uint]*MetricReaderWebSocket),
	}
	http.HandleFunc("/ws_metrics", ms.handleWebSocketConnection)
	logger.Log(NameMetricReaderWebSocketSubFactory, logger.Created, true, true)
	return ms
}

func (f *MetricReaderWebSocketSubFactory) CreateReader(metricClientId uint) MetricReaderBase {
	f.mut.Lock()
	defer f.mut.Unlock()
	reader := NewWebSocketMetricReader()
	f.incompleteConnections[metricClientId] = reader
	return reader
}

func (f *MetricReaderWebSocketSubFactory) handleWebSocketConnection(w http.ResponseWriter, r *http.Request) {
	metricClientIdS := r.URL.Query().Get("metricClientId")
	if metricClientIdS == "" {
		fmt.Println("Metrics: MetricReaderWebSocketSubFactory: No metricClientId provided, returning 400")
		http.Error(w, "No clientID provided", http.StatusBadRequest)
		return
	}
	var metricClientId uint
	_, err := fmt.Sscanf(metricClientIdS, "%d", &metricClientId)
	if err != nil {
		fmt.Printf("Metrics: MetricReaderWebSocketSubFactory: Invalid metricClientId provided: %s, returning 400\n", metricClientIdS)
		http.Error(w, "Invalid clientID provided", http.StatusBadRequest)
		return
	}
	f.mut.Lock()
	reader, ok := f.incompleteConnections[metricClientId]
	if !ok {
		fmt.Printf("Metrics: MetricReaderWebSocketSubFactory: No incomplete connection found for metricClientId %d, returning 404\n", metricClientId)
		http.Error(w, "No incomplete connection found for provided clientID", http.StatusNotFound)
		f.mut.Unlock()
		return
	}
	f.mut.Unlock()
	unsafeWebSocketConn, err := upgrader.Upgrade(w, r, nil)
	if err != nil {
		fmt.Printf("WebRTCSFU: webSocketHandler: ERROR: %s\n", err)
		return
	}

	fmt.Println("WebRTCSFU: webSocketHandler: Websocket handler upgraded")
	reader.SetWebSocketConnection(
		&ThreadSafeWebsocket{
			unsafeWebSocketConn, sync.Mutex{},
		},
	)
	go reader.startListening()

}

func NewWebSocketMetricReader() *MetricReaderWebSocket {
	logger.Log(NameMetricReaderWebSocket, logger.Creating, true, true)
	reader := &MetricReaderWebSocket{}
	logger.Log(NameMetricReaderWebSocket, logger.Created, true, true)
	return reader
}

func (r *MetricReaderWebSocket) SetWebSocketConnection(ws *ThreadSafeWebsocket) {
	r.ws = ws
}

func (r *MetricReaderWebSocket) startListening() {

}

func (r *MetricReaderWebSocket) SetOnDataReceived(onDataReceived func([]byte)) {
	r.onDataReceived = onDataReceived
}
