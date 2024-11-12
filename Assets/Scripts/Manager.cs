using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
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
    public Transform m_fluidPixelHolder;
    public Transform m_character;
    
    private List<CustomCollider> m_colliderUpdateList = new();
    private List<(Vector2, GridHandler.Cell)> m_addIntoGridList = new(); 
    
    private void Awake()
    {
        Me = this;
    }

    private void Start()
    {
        PlayerInventory.Me.StartMe();
        PotionCombiner.Me.StartMe();
        PotionSlotManager.Me.StartMe();
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
    
    public static void AddIntoGrid(Vector2 _pos, GridHandler.Cell _cell)
    {
        Me.m_addIntoGridList.Add((_pos, _cell));
    }
    
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
        if (Input.GetKeyDown(KeyCode.P))
            Time.timeScale = 1f - Time.timeScale;
        if (SaveManager.Me != null && Input.GetKeyDown(KeyCode.R))
            SaveManager.Me.RestartLevel();
        if (SaveManager.Me != null && Input.GetKeyDown(KeyCode.N))
            SaveManager.Me.LevelComplete();
        if (SaveManager.Me != null && Input.GetKeyDown(KeyCode.Escape))
            SaveManager.Me.ExitToMenu();
    }

    private void FixedUpdate()
    {
        GridHandler.Me.CompleteFluidUpdate();
        AddAllIntoGrid();
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

    private void AddAllIntoGrid()
    {
        foreach (var (pos, cell) in m_addIntoGridList)
            GridHandler.Me.AddIntoGrid(pos, cell);
        m_addIntoGridList.Clear();
    }
}
