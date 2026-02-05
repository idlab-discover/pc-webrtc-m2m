using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class PositionPlayback : MonoBehaviour
{
    const string NAME = "PositionPlayback";
    public Transform CameraTransform;
    public Transform ObjectTransform;
    private bool isInited = false;
    private bool isPlayingback = false;
    private PositionPlaybackConfig config;
    private List<PositionRecord> records;
    private float currentPlaybackTime = 0f;
    private PositionRecordReaderBase recordReader;
    private PositionRecord currentRecord;
    private PositionRecord nextRecord;
    private int currentRecordIndex = 0;
    private long startTime;
    void Start()
    {
        
    }

    // Pretty sure this needs to be late, i.e. ensures that it will always be the next frame that has the updated position instead of not knowing for sure
    void LateUpdate()
    {
        if(!isInited || !isPlayingback) return;
        // Lerp between records based on time
        // Slerp for quaternions
        long timeSinceStart = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime;
        //Debug.Log($"{NAME}: Time since start: {timeSinceStart} ms, current record index: {currentRecordIndex}, current playback time: {currentPlaybackTime} ms, nextRecord Time: {nextRecord.timeSinceStartOfRecording}");
        if (timeSinceStart > nextRecord.timeSinceStartOfRecording)
        {
            currentRecordIndex++;
            if(currentRecordIndex >= records.Count -1)
            {
             //   CameraTransform.position = nextRecord.cameraPosition;
            //    CameraTransform.rotation = nextRecord.cameraRotation;
                ObjectTransform.position = nextRecord.objectPosition;
                ObjectTransform.rotation = nextRecord.objectRotation;
                Debug.Log($"{NAME}: Reached end of playback records");
                isPlayingback = false;
                return;
            }
            currentRecord = records[currentRecordIndex];
            nextRecord = records[currentRecordIndex + 1];
            currentPlaybackTime = 0f;
        }
        currentPlaybackTime += Time.deltaTime * 1000;
        // Maybe loop here to find a correct record, 
        float i  = (float)(nextRecord.timeSinceStartOfRecording - currentRecord.timeSinceStartOfRecording);
        if(currentPlaybackTime > i)
        {
            currentPlaybackTime = currentPlaybackTime - i;
            // Hard set position
            CameraTransform.localPosition  = nextRecord.cameraPosition;
            CameraTransform.localRotation = nextRecord.cameraRotation;
            ObjectTransform.position = nextRecord.objectPosition;
            ObjectTransform.rotation = nextRecord.objectRotation;
        } else
        {
            float t = currentPlaybackTime / i;
           // CameraTransform.position = Vector3.Lerp(currentRecord.cameraPosition, nextRecord.cameraPosition, t);
          //  CameraTransform.rotation = Quaternion.Slerp(currentRecord.cameraRotation, nextRecord.cameraRotation, t);
            ObjectTransform.position = Vector3.Lerp(currentRecord.objectPosition, nextRecord.objectPosition, t);
            ObjectTransform.rotation = Quaternion.Slerp(currentRecord.objectRotation, nextRecord.objectRotation, t);
        } 
    }

    public void InitPlayback(PositionPlaybackConfig _config, Transform cameraTransform, Transform objectTransform)
    {
        CameraTransform = cameraTransform;
        ObjectTransform = objectTransform;
        config = _config;
        string fileFormat = "";
        if (string.IsNullOrEmpty(config.inputFile))
        {
            Debug.LogError($"{NAME}: Input file is empty");
            return;
        }
        fileFormat = Path.GetExtension(config.inputFile);
        if (string.IsNullOrEmpty(fileFormat))
        {
            Debug.LogError($"{NAME}: Cannot determine file format from input file {config.inputFile}");
            return;
        }
        fileFormat = fileFormat.TrimStart('.').ToLowerInvariant(); // "json", "csv", "bin", etc.
        // Continious mode only for csv and binary
        // Would be too much overhead for json
        if (config.readContinously && fileFormat == "json")
        {
            Debug.LogError($"{NAME}: Cannot read continously from json file, use csv or binary if you want continous reading");
            return;
        }
        if (!config.readContinously)
        {
            records = new();
        }
     
        switch (fileFormat)
        {
            case "json":
                recordReader = new PositionRecordReaderJSON(config.inputFile);
                break;
            case "csv":
                ///recordWriter = new PositionRecordWriterCSV();
                break;
            case "bin":
               // recordWriter = new PositionRecordWriterBinary(fullOutputPath);
                break;
            default:
                Debug.LogError($"{NAME}: Unknown output format {fileFormat}");
                return;
        }
        startTime = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        records = recordReader.ReadRecords();
        if(records.Count < 2)
        {
            Debug.LogError($"{NAME}: Not enough records to playback");
            return;
        }
        currentRecordIndex = 0;
        currentRecord = records[currentRecordIndex];
        nextRecord = records[currentRecordIndex + 1];
        isInited = true;
    }

    public void StartPlayback()
    {
        isPlayingback = true;
    }

    public void StopPlayback()
    {
        isPlayingback = false;
    }

    void OnDestroy()
    {
        if(!isInited) return;
        recordReader.CloseReader();
    }
}
