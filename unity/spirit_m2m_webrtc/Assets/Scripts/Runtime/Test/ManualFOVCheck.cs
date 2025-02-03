using UnityEngine;
using System.Text;
public class ManualFOVCheck : MonoBehaviour
{
    public Camera camera;
    public GameObject go;

    void Update()
    {
        Debug.Log(ConvertToStringM(camera));
        Debug.Log(WorldToViewportPointManual(go.transform.position, camera));
    }
    string ConvertToStringM(Camera cam)
    {
        Matrix4x4 worldToCameraMatrix = cam.worldToCameraMatrix;
        Matrix4x4 projectionMatrix = cam.projectionMatrix;
        string output = "";
        for(int i = 0; i < 4; i++)
        {
            Vector4 row = worldToCameraMatrix.GetRow(i);
            for (int j = 0; j < 4; j++)
            {
                output += row[j].ToString("0.00000") + ";";
            }
        }
        for (int i = 0; i < 4; i++)
        {
            Vector4 row = projectionMatrix.GetRow(i);
            for (int j = 0; j < 4; j++)
            {
                output += row[j].ToString("0.00000") + ";";
            }
        }
        Vector3 pos = transform.position;
        output += $"{pos.x};{pos.y};{pos.z}";
        return output;
    }
    bool IsObjectInFOV(Vector3 objectPosition, Camera cam)
    {
        // Step 1: Get the camera's intrinsic parameters
        float fov = cam.fieldOfView;
        float aspectRatio = cam.aspect;
        float nearClip = cam.nearClipPlane;
        float farClip = cam.farClipPlane;

        // Step 2: Get the camera's extrinsic parameters
        Matrix4x4 worldToCameraMatrix = cam.worldToCameraMatrix;
       // Debug.Log(cam.WorldToViewportPoint(objectPosition));
       // Debug.Log(WorldToViewportPointManual(objectPosition, cam));
        // Step 3: Convert object position to camera coordinates
        Vector3 objectPositionInCameraSpace = worldToCameraMatrix.MultiplyPoint(objectPosition);

        // Ensure the object is in front of the camera
        if (objectPositionInCameraSpace.z < nearClip || objectPositionInCameraSpace.z > farClip)
        {
            return false;
        }

        // Step 4: Calculate the horizontal and vertical FOV bounds at the object's depth
        float halfVerticalFOV = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * objectPositionInCameraSpace.z;
        float halfHorizontalFOV = halfVerticalFOV * aspectRatio;

        // Step 5: Check if the object's coordinates fall within the FOV bounds
        if (Mathf.Abs(objectPositionInCameraSpace.x) <= halfHorizontalFOV &&
            Mathf.Abs(objectPositionInCameraSpace.y) <= halfVerticalFOV)
        {
            return true;
        }

        return false;
    }
    Vector3 WorldToViewportPointManual(Vector3 worldPosition, Camera cam)
    {
        // Step 1: Get the camera's world-to-camera matrix
        Matrix4x4 worldToCameraMatrix = cam.worldToCameraMatrix;
        
        // Step 2: Convert world position to camera space
        Vector3 cameraSpacePosition = worldToCameraMatrix.MultiplyPoint(worldPosition);

        // Step 3: Get the camera's projection matrix
        Matrix4x4 projectionMatrix = cam.projectionMatrix;
      
        // Step 4: Project the camera space position to NDC
        Vector4 clipSpacePosition = projectionMatrix * new Vector4(cameraSpacePosition.x, cameraSpacePosition.y, cameraSpacePosition.z, 1.0f);
        //   Debug.Log($"{clipSpacePosition}");
        //   Debug.Log($"{projectionMatrix.MultiplyPoint(new Vector3(cameraSpacePosition.x, cameraSpacePosition.y, cameraSpacePosition.z))}");
        
        // Debug.Log($"" +
        //      $"{projectionMatrix.m00} {projectionMatrix.m01} {projectionMatrix.m02} {projectionMatrix.m03}\n" +
        //       $"{projectionMatrix.m10} {projectionMatrix.m11} {projectionMatrix.m12} {projectionMatrix.m13}\n" +
        //       $"{projectionMatrix.m20} {projectionMatrix.m21} {projectionMatrix.m22} {projectionMatrix.m23}\n");
        //Debug.Log($"Test 2 {worldToCameraMatrix}");
        // Step 5: Perform perspective divide to get NDC (Normalized Device Coordinates)
        Vector3 ndcPosition = new Vector3(clipSpacePosition.x / clipSpacePosition.w, clipSpacePosition.y / clipSpacePosition.w, clipSpacePosition.z / clipSpacePosition.w);
        Debug.Log(worldPosition);
        Debug.Log(worldToCameraMatrix);
        Debug.Log(projectionMatrix);
        Debug.Log(cameraSpacePosition);
        Debug.Log(clipSpacePosition);
        Debug.Log(ndcPosition);
        // Step 6: Convert NDC to viewport coordinates
        // NDC ranges from -1 to 1 in X and Y, so we convert it to 0 to 1
        Vector3 viewportPosition = new Vector3(
            (ndcPosition.x + 1.0f) * 0.5f,
            (ndcPosition.y + 1.0f) * 0.5f,
            ndcPosition.z
        );

        return viewportPosition;
    }
}
