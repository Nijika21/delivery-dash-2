using System.Collections.Generic;
using UnityEngine;

public partial class DeliveryTown
{
    [Header("Layout anchors")]
    [SerializeField] private Vector2 depotAnchor = new Vector2(-5f, -6f);
    [SerializeField] private Vector2 startAnchor = new Vector2(5f, -6f);
    [SerializeField] private float startHeading;
    [SerializeField] private Vector2 letterDropoffAnchor = new Vector2(1f, -7f);
    [SerializeField] private Rect finishParkingArea = new Rect(1.75f, -14.5f, 8.5f, 4.5f);
    [SerializeField] private Vector2 plazaAnchor = new Vector2(0f, 55f);
    [SerializeField] private Vector2 plazaSize;
    [SerializeField] private Vector2 drivewayAnchor = new Vector2(0f, -1f);
    [SerializeField] private int tutorialHouseIndex = -1;

    public Vector2 Depot => depotAnchor;
    public Vector2 StartPosition => startAnchor;
    public float StartHeading => startHeading;
    public Vector2 LetterDropoff => letterDropoffAnchor;
    public Rect FinishParkingArea => finishParkingArea;
    public Vector2 PlazaAnchor => plazaAnchor;
    // Lantai parkir gudang (v7); kosong di kota lama.
    public Rect PlazaArea => new Rect(plazaAnchor - plazaSize * 0.5f, plazaSize);
    public Vector2 DrivewayAnchor => drivewayAnchor;

    // v7: pusat truk di dalam kapsul selesai kerja (tepi 496×256 dari 520×280, sudut 108/256 tinggi).
    public bool IsInsideFinishParking(Vector2 position) => finishSize.sqrMagnitude > 0f
        ? StopZoneView.InsideRoundedRect(position, finishCenter, finishSize, Mathf.Min(finishSize.x, finishSize.y) * 108f / 256f, finishAngle)
        : finishParkingArea.Contains(position);

    public void ConfigureAnchors(
        Vector2 depot,
        Vector2 start,
        float heading,
        Vector2 letterDropoff,
        Rect finishParking,
        Vector2 plaza,
        Vector2 driveway,
        int tutorialIndex)
    {
        depotAnchor = depot;
        startAnchor = start;
        startHeading = heading;
        letterDropoffAnchor = letterDropoff;
        finishParkingArea = finishParking;
        plazaAnchor = plaza;
        drivewayAnchor = driveway;
        tutorialHouseIndex = tutorialIndex;
    }

    private Rect[] SplitFinishParkingBays()
    {
        float gap = 0.5f;
        float bayWidth = Mathf.Max(0.1f, (finishParkingArea.width - gap) * 0.5f);
        return new[]
        {
            new Rect(finishParkingArea.xMin, finishParkingArea.yMin, bayWidth, finishParkingArea.height),
            new Rect(finishParkingArea.xMin + bayWidth + gap, finishParkingArea.yMin, bayWidth, finishParkingArea.height)
        };
    }
}
