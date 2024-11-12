using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Manager : MonoBehaviour
{
    public const float c_ParallaxConst = -25/16f;
    public const float c_ParallaxStrength = 0.9f * c_ParallaxConst;
    public const float c_ImageScale = 4f;
    public const float c_CellDiameter = c_ImageScale * 0.01f;
    public const float c_CellRadius = c_CellDiameter * 0.5f;
    
    public static Manager Me;
    
    public RawImage m_background;
    public RectTransform m_combiner;
    public RectTransform m_controls;
    
    private List<CustomCollider> m_colliderUpdateList = new();

    [NonSerialized]
    public float m_lastExplosionTime = -1f;

    private bool m_combinerUp;
    private bool m_controlsUp;
    public bool GUIUp => m_combinerUp || m_controlsUp;
    
    private void Awake()
    {
        Me = this;
    }

    private void Start()
    {
        PlayerInventory.Me.StartMe();
        PotionSlotManager.Me.StartMe();
        m_combinerUp = PotionCombiner.Me.StartMe();
        Time.timeScale = m_combinerUp ? 0f : 1f;
    }

    public void ClickedHelp()
    {
        m_controlsUp = true;
        m_controls.gameObject.SetActive(true);
        Time.timeScale = 0f;
    }

    private void PressedF()
    {
        if (m_controlsUp)
        {
            m_controlsUp = false;
            m_controls.gameObject.SetActive(false);
        }
        else if (m_combinerUp)
        {
            m_combinerUp = !m_combinerUp;
            m_combinerUp &= PotionSlotManager.Me.CanCombine;
            m_combiner.gameObject.SetActive(m_combinerUp);
        }
        Time.timeScale = m_combinerUp ? 0f : 1f;
    }

    private void Clicked()
    {
        if (!IsPointerOverUIElement())
            Character.Me.UpdateThrow();
    }
    
    private bool IsPointerOverUIElement()
    {
        var eventData = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        for (int index = 0; index < results.Count; index++)
        {
            RaycastResult curRaysastResult = results[index];
            if (curRaysastResult.gameObject.layer == LayerMask.NameToLayer("UI"))
                return true;
        }
        return false;
    }

    #region Gizmos
    private interface IGizmo { public void OnDraw(); }
    private class GizmoSquare : IGizmo
    {
        Vector2 m_centre;
        Vector2 m_extents;
        Color32 m_colour;
        
        public GizmoSquare(Vector2 _centre, Vector2 _extents, Color32 _col)
        {
            m_centre = _centre;
            m_extents = _extents;
            m_colour = _col;
        }
        
        public void OnDraw()
        {
            Gizmos.color = m_colour;
            Gizmos.DrawWireCube(m_centre, m_extents);
        }
    }

    private Dictionary<string, List<IGizmo>> m_gizmos = new();

    public void AddGizmoSquare(string _id, Vector2 _centre, Vector2 _extents, Color32 _col)
    {
        var gizmo = new GizmoSquare(_centre, _extents, _col);
        if (m_gizmos.TryGetValue(_id, out var list))
        {
            list.Add(gizmo);
            return;
        }
        m_gizmos.Add(_id, new List<IGizmo>{gizmo});
    }
    
    public void ClearGizmos(string _id)
    {
        if (m_gizmos.TryGetValue(_id, out var list))
            list.Clear();
    }

    private void OnDrawGizmos()
    {
        foreach (var list in m_gizmos.Values)
        {
            foreach (var gizmo in list)
                gizmo.OnDraw();
        }
    }
    #endregion Gizmos
    
    public static void AddCollider(CustomCollider _collider)
    {
        Me.m_colliderUpdateList.Add(_collider);
    }
    
    public static void RemoveCollider(CustomCollider _collider)
    {
        if (Me == null)
            return;
        Me.m_colliderUpdateList.Remove(_collider);
    }

    private void Update()
    {
        GridHandler.Me.CompleteFluidUpdate();
        RunAllCollisions();
        GridHandler.Me.RenderTiles();
        
        m_background.material.SetVector("_Parallax", c_ParallaxStrength * Camera.main.transform.position);
        
#if UNITY_EDITOR
        ClearGizmos("MouseOverBlock");
        var point = (Vector2)Camera.main.ScreenToWorldPoint(Input.mousePosition);
        var pos = GridHandler.Me.DebugPoint(point);
        AddGizmoSquare("MouseOverBlock", pos + c_CellRadius * Vector2.one, c_CellDiameter * Vector2.one, Color.red);
#endif
        
        

        if (Input.GetKeyDown(KeyCode.F))
            PressedF();
        if (Input.GetMouseButtonDown(0))
            Clicked();
        if (SaveManager.Me != null && Input.GetKeyDown(KeyCode.R))
            SaveManager.Me.RestartLevel();
        if (SaveManager.Me != null && Input.GetKeyDown(KeyCode.Escape))
            SaveManager.Me.ExitToMenu();
    }

    private void FixedUpdate()
    {
        GridHandler.Me.CompleteFluidUpdate();
        RunAllCollisions();
        GridHandler.Me.UpdateFluids();
        Character.Me.FixedUpdateMe();
    }

    private void RunAllCollisions()
    {
        var bounds = new GridHandler.ColliderData[m_colliderUpdateList.Count];
        for (var index = 0; index < m_colliderUpdateList.Count; index++)
        {
            var coll = m_colliderUpdateList[index];
            var min = GridHandler.Me.GetCellFloat(coll.m_bounds.min);
            var max = GridHandler.Me.GetCellFloat(coll.m_bounds.max);
            var prevBounds = coll.GetPrevBounds();
            var prevMin = GridHandler.Me.GetCellFloat(prevBounds.min);
            var prevMax = GridHandler.Me.GetCellFloat(prevBounds.max);
            var nextBound = new GridHandler.ColliderData(min, max, prevMin, prevMax, coll.GetVelocity());
            bounds[index] = nextBound;
        }
        
        GridHandler.Me.CheckCells(ref bounds);
        
        for (var index = 0; index < m_colliderUpdateList.Count; index++)
        {
            var coll = m_colliderUpdateList[index];
            var result = bounds[index];
            var newPos = GridHandler.Me.GetPositionFloat(result.GetCentre);
            coll.UpdateWithResults(newPos, result.m_velocity, result.m_bounceAround, result.m_slimeAround, result.m_blocksAround);
        }
    }
}
