# WebRTC SFU and client applications
This repository contains the code to build both the WebRTC SFU (server) and peer (client) applications. Both make use of the [pion](https://github.com/pion/webrtc) as the backbone implementation of the whole WebRTC stack. Signalling (i.e., exchange of the SDP and ICE candidates) is performed using a Websocket server that is included in the SFU. This Websocket server is also used to transmit the current position and field of view of the client to the server.

The SFU server contains an bitrate allocation algorithm which uses the bandwidth estimated by GCC to decide which quality of each user has to be send to the client. Additionally, it uses position and field of view of the client to improve overall quality by giving more quality to those users that are closer to the client.

For a more detailed explanation on how this algorithm works, we refer to our recent publications [1, 2].

[1] M. De Fré, J. van der Hooft, T. Wauters, and F De Turck. "Demonstrating Adaptive Many-to-Many Immersive Teleconferencing for Volumetric Video", Proceedings of the 15th ACM Multimedia Systems Conference, 2024 (available [here](https://backoffice.biblio.ugent.be/download/01HW2J0M02RWJSSFSGP8EEDQ1B/01HW2J41RKP8CXHFTR22D2ARNQ))

[2] M. De Fré, J. van der Hooft, T. Wauters, and F De Turck. "Scalable MDC-Based Volumetric Video Delivery for Real-Time One-to-Many WebRTC Conferencing", Proceedings of the 15th ACM Multimedia Systems Conference, 2024 (available [here](https://backoffice.biblio.ugent.be/download/01HW2J66EZD49XQD2P94JBXHKR/01HW2J8F937QNC36XHZEBRHE8K))

## Usage
#### Client
> ❗ Remember to place the peer executable in the correct Unity folder after building!
Normally, you will never have to manually run the peer application as it is automatically started by Unity. However, below you can find the parameters that are used by the Unity application to configure the peer. 

| Argument        | Full Name         | Description                                                   | Default Value     |
|-----------------|-------------------|---------------------------------------------------------------|-------------------|
| `--sfu`          | SFU Address       | Address of the SFU server.                                     | `localhost:8080`  |
| `-p`            | Proxy Port        | Port through which the DLL is connected.                       | `:8000`              |
| `-i`            | Use Proxy Input   | Receive content from the DLL to forward over WebRTC.           | `false`           |
| `-o`            | Use Proxy Output  | Forward content received over WebRTC to the DLL.               | `false`           |
| `-c`            | Client ID         | Unique identifier for the client.                              | `0`               |
| `-t`            | Number of Tiles   | Specifies the number of tiles to be handled.                   | `1`               |

##### Server
Below are the possible parameters you can use to configure the SFU:
| Argument        | Full Name         | Description                                                   | Default Value     |
|-----------------|-------------------|---------------------------------------------------------------|-------------------|
| `--addr`          | SFU Address       | Which address + port to use (0.0.0.0 for all interfaces).                                     | `0.0.0.0:8080`  |
| `-d`            | Disable GCC        | Disables GCC bandwidth estimation and allows you to manually control bandwidth limits via the dashboard.                       | `false`              |
| `-t`            | Number of maximum tracks    | The number of video tracks (in this case descriptions) a client is allowed to transmit.           | `1`           |
##### Server dashboard
You can also access a dashboard in your browser by using the same address you used for the SFU, followed by `/dashboard`. This dashboard gives you access to certain statistics of each user such as: what clients can the user see, what quality is send to this user, what bitrate is send to this user. If you disable GCC, you will also get access to a bandwidth limiter that restricts the maximum bandwidth that can be assigned by the bitrate allocation algorithm.

TODO add dashboard screenshot
## Building
Simply use: `go build -o sfu.exe ./sfu` (similar for the peer) to build the application.

## Dependencies
You do not need to worry about any dependancies as Golang will automatically download them for you.
