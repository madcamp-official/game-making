using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캔버스 배율을 정수로 고정한다.
///
/// 기본 <see cref="CanvasScaler"/>의 Scale With Screen Size는 화면 크기에 비례해 1.33배 같은
/// 소수 배율을 만드는데, 비트맵 폰트는 그런 배율에서 획 굵기가 픽셀마다 달라져 뭉개져 보인다.
/// 그래서 Constant Pixel Size로 두고 배율을 화면 높이에 따라 1, 2, 3배로만 올린다.
/// </summary>
[RequireComponent(typeof(CanvasScaler))]
[ExecuteAlways]
public class PixelPerfectCanvasScaler : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxScale = 4;

    private CanvasScaler scaler;
    private int lastHeight = -1;

    private void OnEnable()
    {
        scaler = GetComponent<CanvasScaler>();
        Apply();
    }

    private void Update()
    {
        if (Screen.height != lastHeight) Apply();
    }

    private void Apply()
    {
        if (scaler == null) scaler = GetComponent<CanvasScaler>();
        lastHeight = Screen.height;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = Mathf.Min(PixelUi.PixelScale, maxScale);
    }
}
