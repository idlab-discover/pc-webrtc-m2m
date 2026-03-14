using UnityEngine;
using TMPro;

public class NameTagRenderer : MonoBehaviour
{
    [SerializeField] private string nameText = "Player";
    [SerializeField] private float heightOffset = 2f;
    [SerializeField] private float fontSize = 3f;
    [SerializeField] private Color textColor = Color.white;

    private Transform _labelTransform;
    private TextMeshPro _textMesh;
    private Transform _cameraTransform;

    private void Awake()
    {
        SetupLabel();
        CacheCamera();
    }

    private void SetupLabel()
    {
        var labelGO = new GameObject("_NameTag");
        labelGO.transform.SetParent(transform, false);
        labelGO.transform.localPosition = new Vector3(0f, heightOffset, 0f);

        _textMesh = labelGO.AddComponent<TextMeshPro>();
        _textMesh.text = nameText;
        _textMesh.fontSize = fontSize;
        _textMesh.color = textColor;
        _textMesh.alignment = TextAlignmentOptions.Center;
        _textMesh.enableWordWrapping = false;

        _labelTransform = labelGO.transform;
    }

    private void CacheCamera()
    {
        if (Camera.main != null)
            _cameraTransform = Camera.main.transform;
    }

    // LateUpdate ensures the billboard runs after all movement/animation
    private void LateUpdate()
    {
        if (_cameraTransform == null)
        {
            CacheCamera();
            return;
        }
        _labelTransform.rotation = _cameraTransform.rotation;
    }

    /// <summary>
    /// Updates the displayed name. Uses TMP's internal buffer to avoid string allocation.
    /// </summary>
    public void SetName(string name)
    {
        nameText = name;
        if (_textMesh != null)
            _textMesh.SetText(name);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_textMesh == null) return;
        _textMesh.text = nameText;
        _textMesh.fontSize = fontSize;
        _textMesh.color = textColor;
        if (_labelTransform != null)
            _labelTransform.localPosition = new Vector3(0f, heightOffset, 0f);
    }
#endif
}
