
public struct CompMDCRemoteFrameReceived
{
    public long receivingTimestamp;
    public uint clientID;
    public uint capturerID;
    public uint frameNr;
    public uint descriptionID;
    public uint dataBufferSize;
    public uint totalNumberOfPoints;
}
public struct CompMDCDecodingDone
{
    public long decodingDoneTimestamp;
    public uint clientID;
    public uint capturerID;
    public uint frameNr;
    public uint descriptionID;
}

// Maybe we add capturing timestamp here in the future, but it is pretty redundant atm
public struct CompMDCReadyToRender
{
    public long readyToRenderTimestamp;
    public uint clientID;
    public uint frameNr;
    public uint totalNumberOfPoints;
    public uint qualityLevel;
}

public struct CompMDCLateReceived
{
    public long lateReceivedTimestamp;
    public uint clientID;
    public uint capturerID;
    public uint frameNr;
    public uint descriptionID;
}