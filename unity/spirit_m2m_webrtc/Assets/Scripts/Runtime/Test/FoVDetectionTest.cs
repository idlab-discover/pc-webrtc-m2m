using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class FoVDetectionTest : MonoBehaviour
{
    private PlayerControllerBase playerController;
    public List<Renderer> ObjectsToDetect = new();
    // Start is called before the first frame update
    public void Init(PlayerControllerBase playerController)
    {
        Debug.Log("FoVDetectionTest initialized");
        this.playerController = playerController;
        playerController.OnPlayerControllerPositionUpdated += onPlayerControllerPositionUpdated;
    }

    private void onPlayerControllerPositionUpdated(ClientPositionUpdate newPostion)
    {
        Debug.Log($"Position updated: x {newPostion.position[0]} y {newPostion.position[1]} z {newPostion.position[2]}");
        foreach(var obj in ObjectsToDetect)
        {
            setQualityColor(obj, newPostion);
        }
    }
    void OnDestroy()
    {
        playerController.OnPlayerControllerPositionUpdated -= onPlayerControllerPositionUpdated;
    }
    private void setQualityColor(Renderer renderer, ClientPositionUpdate positionUpdate)
    {
        uint quality = calculatePointVisibility(positionUpdate.worldToCameraMatrix, positionUpdate.projectionMatrix, renderer.gameObject.name, renderer.transform.position, 3);
        switch (quality) {
            case 0:
                renderer.material.color = Color.green;
                break;
            case 1:
                renderer.material.color = Color.yellow;
                break;
            case 2:
                renderer.material.color = Color.red;
                break;
            default:
                renderer.material.color = Color.magenta;
                break;
        }
    }

    private Vector3 multiplyPoint(float[,] m, Vector3 p)
    {
        float x = m[0, 0] * p.x + m[0, 1] * p.y + m[0, 2] * p.z + m[0, 3];
        float y = m[1, 0] * p.x + m[1, 1] * p.y + m[1, 2] * p.z + m[1, 3];
        float z = m[2, 0] * p.x + m[2, 1] * p.y + m[2, 2] * p.z + m[2, 3];
        return new Vector3(x, y, z);
    }

    private Vector4 convertToClipspace(float[,] m, Vector3 p)
    {
        float x = m[0, 0] * p.x + m[0, 1] * p.y + m[0, 2] * p.z + m[0, 3];
        float y = m[1, 0] * p.x + m[1, 1] * p.y + m[1, 2] * p.z + m[1, 3];
        float z = m[2, 0] * p.x + m[2, 1] * p.y + m[2, 2] * p.z + m[2, 3];
        float w = m[3, 0] * p.x + m[3, 1] * p.y + m[3, 2] * p.z + m[3, 3];
        return new Vector4(x, y, z, w);
    }

    private uint calculatePointVisibility(float[,] camMatrix, float[,] projectionMatrix, string objName, Vector3 p, uint nBands)
    {
        Vector3 camSpace = multiplyPoint(camMatrix, p);
        Vector4 clipSpace = convertToClipspace(projectionMatrix, camSpace);
        Vector3 ndcSpace = new Vector3(
            clipSpace.x / clipSpace.w,
            clipSpace.y / clipSpace.w,
            clipSpace.z / clipSpace.w
        );

        Debug.Log($"CalculatePointVisibility {objName}: Pos x {ndcSpace.x} y {ndcSpace.y} z {ndcSpace.z}");

        float bandSpacing = 1.0f / nBands;
        for (uint i = 0; i < nBands; i++)
        {
            if (ndcSpace.x >= -(bandSpacing * (i + 1)) && ndcSpace.x <= bandSpacing * (i + 1))
                return i;
        }

        if (ndcSpace.x >= -1.25f && ndcSpace.x <= 1.25f)
            return nBands - 1;

        return nBands;
    }
}
