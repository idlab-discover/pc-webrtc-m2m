# Simple Camera Example
You can find a simple scene (in `Scenes/ExampleScenes/ExampleCamera` with the corresponding script being in `Scripts/Runtime/ExampleScripts/ExampleCamera`)<sup>[code](../Assets/Scripts/Runtime/ExampleScripts/ExampleCamera.cs)</sup> which utilizes our [capturer library](../../../point_cloud_capturer) with one camera to render a point cloud in the scene.

The script also uses several other scripts to allow for cleaner code, be sure to look at the workings of `SingleCapture`<sup>[code](../Assets/Scripts/Runtime/Capture/SingleCapture.cs)</sup>, `SimplePointCloud`<sup>[code](../Assets/Scripts/Runtime/Frame/SimplePointCloud.cs)</sup> and `SimpleRenderablePointCloudBuffer`<sup>[code](../Assets/Scripts/Runtime/Frame/SimpleRenderablePointCloudBuffer.cs)</sup>, to fully understand the inner workings. 


:warning: This example does rely on using a `SessionInfo`, for which you can find an example in `Assets/Config/session_config`<sup>[config](../Assets/config/session_config.json)</sup>.




