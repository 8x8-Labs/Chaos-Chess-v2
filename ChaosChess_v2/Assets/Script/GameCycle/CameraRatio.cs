using UnityEngine;

public class CameraRatio : MonoBehaviour
{
    [SerializeField] private float boardSize = 6f;
    private Camera cam;
    void Start()
    {
        cam = GetComponent<Camera>();
        AdjustCamera();        
    }

    void AdjustCamera()
    {
        float aspect = (float)Screen.width / Screen.height;

        // 가로가 더 좁으면 가로 기준으로 보정
        if (aspect < 1f)
            cam.orthographicSize = (boardSize / 2f) / aspect;
        else
            cam.orthographicSize = boardSize / 2f;
    }
}
