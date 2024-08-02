# SPIRIT WebRTC

In this repository, all files related to a multi-party volumetric video-based system are made available. Three parts are considered:

- A [Unity project](unity) written in C#, used to create sessions and render point cloud video
- A [WebRTC framework](webrtc) written in Golang, used to interconnect peers through a selective forwarding unit (SFU)
- A [connector plugin](connector) written in C++, used to interconnect the Unity application to the WebRTC client (currently incomptabile)

The system is currently under development by IDLab, Ghent University - imec. This README will be updated while development continues, with detailed instructions for each of these components.

## Architecture

The developed system supports multi-party point cloud delivery using a multi-description coding (MDC) approach. In contrast to traditional approaches - which either compress point cloud frames as a whole or apply spatial segmentation in the form of tiles - our solution creates several distinct subsets (descriptions) of sampled points. Each of these descriptions can be encoded separately, resulting in lower processing times due to parallelization. Furthermore, multiple descriptions can be merged together after decoding, resulting in a representation of higher visual quality.

![Architecture](architecture.png)

As shown in the above illustration, our solution provides a fixed number of encoders per participant, corresponding to number of descriptions. The number of local decoders for each client scales with the number of received descriptions. The SFU unit decides on what descriptions will be forwarded to each client, taking into account the user's field of view (FoV) and position in the sence, as well as the bandwidth that is available to the client (estimated through [Google congestion control](https://datatracker.ietf.org/doc/html/draft-ietf-rmcat-gcc-02)).

For a more detailed explanation of the system, we refer to our recent publications [1, 2].

[1] M. De Fré, J. van der Hooft, T. Wauters, and F De Turck. "Demonstrating Adaptive Many-to-Many Immersive Teleconferencing for Volumetric Video", Proceedings of the 15th ACM Multimedia Systems Conference, 2024 (available [here](https://backoffice.biblio.ugent.be/download/01HW2J0M02RWJSSFSGP8EEDQ1B/01HW2J41RKP8CXHFTR22D2ARNQ))

[2] M. De Fré, J. van der Hooft, T. Wauters, and F De Turck. "Scalable MDC-Based Volumetric Video Delivery for Real-Time One-to-Many WebRTC Conferencing", Proceedings of the 15th ACM Multimedia Systems Conference, 2024 (available [here](https://backoffice.biblio.ugent.be/download/01HW2J66EZD49XQD2P94JBXHKR/01HW2J8F937QNC36XHZEBRHE8K))

## Installation

TODO

## Usage

TODO

## Funding

This work is funded by the European Union [SPIRIT project](https://www.spirit-project.eu), grant agreement 101070672.

![Logo](logo.png)