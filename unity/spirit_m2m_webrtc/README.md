# Spirit Unity many-to-many streaming
The repository contains the Unity project that serves as the client in the many-to-many streaming pipeline. This application is responsible for capturing, encoding, decoding and transmitting the encoded data to the SFU. 

As Unity does not have solid support for WebRTC, a seperate Golang based application is used. This application allows us to make use of advanced WebRTC features, such as GCC based bandwidth estimation and NACK retransmissions. Unity communicates with this external application (which is automatically started by Unity) using sockets.
## Project structure

- Scenes: contains the scenes of the project (you will mostly need to use `MainScene`)
- Scripts: contains the C# scripts used by the application
- Prefabs: contains the Unity `GameObject` prefabs for sending and receiving
- Plugins: contains all the Dlls used by the application

## Building
Building the application is very simple, and is the same as building a normal Unity application. 

However, after building the application, you will have to manually copy the `config` and `peer` directories from the `Assets` directory to the [Unity application datapath](https://docs.unity3d.com/ScriptReference/Application-dataPath.html) (In Windows this is: `spirit_unity_Data`).

## Usage
The arrow keys can be used to move the camera when not using any headset. If you are using a headset, make sure that headset is fully connected to your pc (e.g., for Meta Quest, make sure you are fully linked before starting the application).

#### Config file
You can configure the application, including the camera, by changing the values of `session_config.json`. Below is a list of all possible parameters:

| Parameter             | Description |
|-----------------|-------|
| `clientID`      |   The id of the client, this will determine your position at the table    |
| `sfuAddress`    |   The address (ip + port) of the SFU server    |
| `peerUDPPort`   |   The port that will be used to communicate with the Golang client application    |
| `startPositions`| An array containing the positions of every user around the table      |
| `table.position`| The position of the table      |
| `table.scale`   | The scale of the table      |
| `camClose`      | The close clip plane of the camera, any points before this value (meters) will be ignored      |
| `camFar`        | The far clip plane of the camera, any points beyond this value (meters) will be ignored       |
| `camWidth`      |  The width (pixels) of the camera frame    |
| `camHeight`     |  The height (pixels) of the camera frame     |
| `camFPS`        |  The frame rate of the camera, the possible values of this depend on the camera you are using     |
| `useCam`        | Setting this to false will replace the camera with an artifical point cloud in the shape of a cube       |


## Dependencies
All dependencies will be automatically downloaded when opening the project.

However, you do need the two Dlls responsible for [capturing](../capturer) and [encoding/decoding](../mdc_encoder). You will only need to worry this about you make changes to these Dlls, and when you are building the application (see building instructions).

## Supported HMDs
In general every OpenXR compatible headset will work. However, below is a list of all headsets that have been tested and verified:
#### Tested
- Meta Quest 2
