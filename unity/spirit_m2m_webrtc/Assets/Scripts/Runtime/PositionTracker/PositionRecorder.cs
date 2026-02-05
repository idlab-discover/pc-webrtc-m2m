using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PositionRecorder : MonoBehaviour
{
    const string NAME = "PositionRecorder";
    public Transform CameraTransform;
    public Transform ObjectTransform;
    private bool isInited = false;
    private bool isRecording = false;
    private PositionTrackerConfig config;
    private List<PositionRecord> records;
    private float currentRecordTime = 0f;
    private long startTime;
    private PositionRecordWriterBase recordWriter;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (!isInited || !isRecording) return;
        if (CameraTransform == null || ObjectTransform == null) return;
        
        currentRecordTime += Time.deltaTime;
        if(currentRecordTime < config.recordInterval) return;
        
        currentRecordTime = 0f;
        AddRecord();
    }

    void AddRecord()
    {
        // TODO Check if we need to use local or world here
        Vector3 cameraPos = CameraTransform.localPosition;
        Quaternion cameraRot = CameraTransform.localRotation;
        Vector3 objectPos = ObjectTransform.position;
        Quaternion objectRot = ObjectTransform.rotation;
        PositionRecord record = new PositionRecord
        {
            timeSinceStartOfRecording = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime,
            cameraPosition = cameraPos,
            cameraRotation = cameraRot,
            objectPosition = objectPos,
            objectRotation = objectRot
        };
        if (!config.writeContinously)
        {
            records.Add(record);
            return;
        }
        recordWriter.WriteRecord(ref record);
    }

    public void InitRecording(PositionTrackerConfig _config, Transform cameraTransform, Transform objectTransform)
    {
        CameraTransform = cameraTransform;
        ObjectTransform = objectTransform;
        config = _config;
        // Continious mode only for csv and binary
        // Would be too much overhead for json
        if (config.writeContinously && config.outputFormat == "json")
        {
            Debug.LogError($"{NAME}: Cannot write continously and output to json, use csv or binary if you want continous writing");
        }
        if(!config.writeContinously)
        {
            records = new();
        }
        if(config.outputPath == null || config.outputPath == "")
        {
            Debug.LogError($"{NAME}: Output path is not set");
            return;
        }
        string fullOutputPath = $"{config.outputPath}/{config.outputFileName}";
        if(config.addTimestampToPath)
        {
            string timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            fullOutputPath += $"_{timestamp}";
        }
        fullOutputPath += $".{config.outputFormat.ToLower()}";

        switch (_config.outputFormat.ToLower())
        {
            case "json":
                recordWriter = new PositionRecordWriterJSON(fullOutputPath);
                break;
            case "csv":
                ///recordWriter = new PositionRecordWriterCSV();
                break;
            case "bin":
                recordWriter = new PositionRecordWriterBinary(fullOutputPath);
                break;
            default:
                Debug.LogError($"{NAME}: Unknown output format {_config.outputFormat}");
                return;
        }
        startTime = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        AddRecord(); // Add initial record
        
        isInited = true;
    }
    public void StartRecording()
    {
        isRecording = true;
    }
    public void StopRecording()
    {
        isRecording = false;
    }

    public void OnDestroy()
    {
        if(!isInited || config.writeContinously) return;
        recordWriter.WriteRecords(records);
        recordWriter.CloseWriter();
    }

}
