using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

public class PointCloudRenderer : MonoBehaviour
{
    private const string NAME = "PointCloudRenderer";
    [SerializeField]
    private List<GameObject> renderers = new();
    private List<MeshFilter> meshFilters = new();

    [SerializeField]
    private List<uint> qualityBreakpoints = new();

    private uint currentQualityIndex = 0;
    private uint currentQuality = 0;
    private Mesh currentMesh;
    void Start()
    {
        meshFilters = new(renderers.Count);
        foreach (var p in renderers)
        {
            meshFilters.Add(p.GetComponent<MeshFilter>());
        }
        if(renderers.Count > 0)
        {
            renderers[0].SetActive(true);
        }
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void SetPointCloud(RenderablePointCloud dec)
    {
        Logger.LogPCFrameStatusLimited(NAME, Logger.Status.FrameStartRendering, 0, dec.FrameNr);
       // setQuality(dec.Quality);
        Destroy(currentMesh);
        currentMesh = new Mesh();
        currentMesh.indexFormat = dec.TotalPoints > 65535 ?
                IndexFormat.UInt32 : IndexFormat.UInt16;
        currentMesh.SetVertices(dec.Points);
        currentMesh.SetColors(dec.Colors);
        currentMesh.SetIndices(
            Enumerable.Range(0, currentMesh.vertexCount).ToArray(),
            MeshTopology.Points, 0
        );
      
        currentMesh.UploadMeshData(true);
        meshFilters[(int)currentQualityIndex].mesh = currentMesh;
        Logger.LogPCFrameStatusLimited(NAME, Logger.Status.FrameRendered, 0, dec.FrameNr);
    }
    public void ClearRenderer()
    {

    }
    private void setQuality(uint quality)
    {
        if(currentQuality == quality) return;

        uint selectedQualityIndex = 0;
        for (uint i= 0; i < qualityBreakpoints.Count; i++)
        {
            if (qualityBreakpoints[(int)i] > quality)
            {
                break;
            }
            selectedQualityIndex = i;
        }
        if (currentQualityIndex == selectedQualityIndex) return;

        for(int i = 0; i < renderers.Count; i++)
        {
            if (i == selectedQualityIndex)
            {
                renderers[i].SetActive(true);
            }
            else
            {
                renderers[i].SetActive(false);
            }
        }
    }
}
