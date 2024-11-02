using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Random = Unity.Mathematics.Random;

public class GridHandler : MonoBehaviour
{
    public const float c_ImageScale = 4f;
    public const float c_CellDiameter = c_ImageScale * 0.01f;
    public const float c_CellInverseD = 1f / c_CellDiameter;

    public static GridHandler Me;
    
    public struct Cell
    {
        public enum Type
        {
            Empty,
            Wall,
            Water,
            Sand,
            Lava,
            Wood,
            Fire,
            Smoke,
            Steam,
            Acid,
            Gas,
            Oil,
            Tar,
            Ice
        }
        
        public Type m_type;
        public float m_amount;

        public bool IsLiquid(NativeArray<CellProperties> _properties)
        {
            return m_type != Type.Empty && _properties[(int)m_type].viscosity < 1f;
        }
        
        public Cell TryAdd(Cell _otherCell, out bool _success)
        {
            _success = true;
            if (m_type == Type.Empty || m_type == _otherCell.m_type)
                return Add(_otherCell.m_type, _otherCell.m_amount);
            
            _success = false;
            return this;
        }

        public Cell Add(Type _type, float _amount)
        {
            return new Cell {m_type = _type, m_amount = m_amount + _amount};
        }
    }
    
    [Serializable] public struct CellProperties 
    { 
        public Cell.Type type; //Only here for inspector
        public Color32 colour;
        public float viscosity;
        public float flammability;
    }
    [SerializeField] private List<CellProperties> m_cellProperties;
    private NativeArray<CellProperties> m_cellPropertiesNative;

    [SerializeField] private RectTransform m_image;
    [SerializeField] private Texture2D m_level;
    [SerializeField] private Texture2D m_textureHolder;
    private int m_levelWidth;
    private int m_levelHeight;
    
    private NativeArray<Cell> m_cells;
    private NativeHashSet<int2> m_fluidCells;
    
    private void Awake()
    {
        Me = this;
    }

    private void Start()
    {
        m_levelWidth = m_level.width;
        m_levelHeight = m_level.height;
        m_image.localScale = new Vector3(c_ImageScale, c_ImageScale, 1f);
        m_image.sizeDelta = new Vector2(m_levelWidth, m_levelHeight);
        m_textureHolder = new Texture2D(m_levelWidth, m_levelHeight, TextureFormat.RGBA32, 0, true)
        {
            filterMode = FilterMode.Point
        };
        m_image.GetComponent<RawImage>().material.SetTexture("_MainTex", m_textureHolder);

        m_cellPropertiesNative = new(m_cellProperties.Count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        for (int i = 0; i < m_cellProperties.Count; ++i) //I will optimise if needed but should be a short array
            m_cellPropertiesNative[i] = m_cellProperties[i];

        m_cells = new NativeArray<Cell>(m_levelWidth * m_levelHeight, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        var job = new FillMapJob(m_cells, m_level);
        job.Schedule(m_levelWidth * m_levelHeight, 64).Complete();
        
        m_fluidCells = new NativeHashSet<int2>(64, Allocator.Persistent);
        var findFluidsJob = new FindFluidsJob(m_cells, m_cellPropertiesNative, m_fluidCells, m_levelWidth, m_levelHeight);
        findFluidsJob.Schedule().Complete();
    }

    [BurstCompile]
    private struct FillMapJob : IJobParallelFor
    {
        private NativeArray<Cell> m_cells;
        [ReadOnly] private NativeArray<byte> m_texture;

        public FillMapJob(NativeArray<Cell> _cells, Texture2D _image)
        {
            m_cells = _cells;
            m_texture = _image.GetRawTextureData<byte>();
        }
        
        public void Execute(int _index)
        {
            var r = m_texture[4 * _index];
            var g = m_texture[4 * _index + 1];
            var b = m_texture[4 * _index + 2];
            var a = m_texture[4 * _index + 3];

            var type = (r / 64) * 16 + (g / 64) * 4 + (b / 64);
            
            m_cells[_index] = new Cell { m_type = (Cell.Type)type, m_amount = (a + 1) * 0.00390625f }; // 1/256
        }
    }
    
    [BurstCompile]
    private struct FindFluidsJob : IJob
    {
        private NativeHashSet<int2> m_fluidCells;
        [ReadOnly] private NativeArray<Cell> m_cells;
        [ReadOnly] private NativeArray<CellProperties> m_cellProperties;
        [ReadOnly] private int m_width, m_height;

        public FindFluidsJob(NativeArray<Cell> _cells, NativeArray<CellProperties> _properties, NativeHashSet<int2> _fluidCells, int _width, int _height)
        {
            m_cells = _cells;
            m_cellProperties = _properties;
            m_fluidCells = _fluidCells;
            m_width = _width;
            m_height = _height;
        }
        
        public void Execute()
        {
            for (int i = 0; i < m_width; ++i)
            {
                for (int j = 0; j < m_height; ++j)
                {
                    var cell = m_cells[i + j * m_width];
                    if (cell.IsLiquid(m_cellProperties))
                        m_fluidCells.Add(new(i, j));
                }
            }
        }
    }

    private void Update()
    {
        var job = new RenderJob(m_cells, m_cellPropertiesNative, m_levelWidth, m_levelHeight);
        job.Schedule(m_levelWidth * m_levelHeight, 64).Complete();
        job.SetTexture(m_textureHolder);

        if (Input.GetMouseButtonDown(1))
        {
            var point = (Vector2)Camera.main.ScreenToWorldPoint(Input.mousePosition);
            var index = GetCell(point, Mathf.RoundToInt);
            var cell = m_cells[index.x + index.y * m_levelWidth];
            Debug.LogError($"Clicked on {index} which is {cell.m_type} with {cell.m_amount}. Is registered: {m_fluidCells.Contains(index)}");
        }
    }

    [BurstCompile]
    private struct RenderJob : IJobParallelFor
    {
        [ReadOnly] private NativeArray<Cell> m_cells;
        [ReadOnly] private NativeArray<CellProperties> m_cellProperties;
        [ReadOnly] private int m_width, m_height;
        [NativeDisableParallelForRestriction] private NativeArray<byte> m_texture;

        public RenderJob(NativeArray<Cell> _cells, NativeArray<CellProperties> _properties, int _width, int _height)
        {
            m_cells = _cells;
            m_cellProperties = _properties;
            m_texture = new NativeArray<byte>(_width * _height * 4, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            m_width = _width;
            m_height = _height;
        }
        
        public void Execute(int _index)
        {
            var cell = m_cells[_index];
            var type = (int)cell.m_type;
            var properties = m_cellProperties[type];
            
            m_texture[4 * _index] = (byte)type;
            m_texture[4 * _index + 1] = GetNeighbourStatus(cell.m_type, _index);
            m_texture[4 * _index + 2] = (byte)(255 * properties.viscosity);
            m_texture[4 * _index + 3] = (byte)(Mathf.Clamp01(cell.m_amount) * properties.colour.a);
        }

        private byte GetNeighbourStatus(Cell.Type _type, int _index, bool _matchType = false)
        {
            int x = _index % m_width;
            int y = _index / m_width;

            var left = new int2(x - 1, y);
            var right = new int2(x + 1, y);
            var up = new int2(x, y + 1);
            var down = new int2(x, y - 1);
            
            byte status = 0;
            if (left.x >= 0)
            {
                var leftCell = m_cells[left.x + left.y * m_width];
                if (_matchType ? leftCell.m_type == _type : leftCell.m_type != Cell.Type.Empty)
                    status |= 1 << 3;
            }
            if (right.x < m_width)
            {
                var rightCell = m_cells[right.x + right.y * m_width];
                if (_matchType ? rightCell.m_type == _type : rightCell.m_type != Cell.Type.Empty)
                    status |= 1 << 2;
            }
            if (up.y < m_height)
            {
                var upCell = m_cells[up.x + up.y * m_width];
                if (_matchType ? upCell.m_type == _type : upCell.m_type != Cell.Type.Empty)
                    status |= 1 << 1;
            }
            if (down.y >= 0)
            {
                var downCell = m_cells[down.x + down.y * m_width];
                if (_matchType ? downCell.m_type == _type : downCell.m_type != Cell.Type.Empty)
                    status |= 1 << 0;
            }

            return status;
        }

        public void SetTexture(Texture2D _texture)
        {
            _texture.SetPixelData(m_texture, 0, 0);
            _texture.Apply();
            
            m_texture.Dispose();
        }
    }
    
    public CellProperties GetProperties(Cell _cell)
    {
        return m_cellProperties[(int)_cell.m_type];
    }
    
    public int2 GetCell(Vector2 _p, Func<float, int> Clamp)
    {
        var x = Mathf.Clamp(Clamp(m_levelWidth * 0.5f + c_CellInverseD * _p.x), 0, m_levelWidth - 1);
        var y = Mathf.Clamp(Clamp(m_levelHeight * 0.5f + c_CellInverseD * _p.y), 0, m_levelHeight - 1);
        return new(x, y);
    }
    
    private Vector2 GetPosition(Vector2 _i)
    {
        _i.x -= m_levelWidth >> 1;
        _i.y -= m_levelHeight >> 1;
        return _i * c_CellDiameter;
    }

    public Vector3[] CheckCells(Bounds _bounds)
    {
        int2 si = GetCell(_bounds.min, Mathf.FloorToInt); //Start integers
        int2 ei = GetCell(_bounds.max, Mathf.CeilToInt); //End integers
        
        var job = new CheckCellsJob(m_cells, m_cellPropertiesNative, si.x, si.y, ei.x, ei.y, m_levelWidth);
        job.Schedule().Complete();
        var output = job.GetOutput();
        for (int i = 0; i < output.Length; ++i)
            output[i] = GetPosition(output[i]).AddZ(output[i].z);
        return output;
    }


    [BurstCompile]
    private struct CheckCellsJob : IJob
    {
        [ReadOnly] private NativeArray<Cell> m_cells;
        [ReadOnly] private NativeArray<CellProperties> m_cellProperties;
        [ReadOnly] private int m_sx, m_sy, m_ex, m_ey;
        [ReadOnly] private int m_width;
        private NativeList<float3> m_output;

        public CheckCellsJob(NativeArray<Cell> _cells, NativeArray<CellProperties> _properties, int _sx, int _sy, int _ex, int _ey, int _width)
        {
            m_cells = _cells;
            m_cellProperties = _properties;
            m_sx = _sx;
            m_sy = _sy;
            m_ex = _ex;
            m_ey = _ey;
            m_width = _width;
            m_output = new((m_ex - m_sx + 1) * (m_ey - m_sy + 1), Allocator.TempJob);
        }

        public void Execute()
        {
            for (int x = m_sx; x <= m_ex; ++x)
            {
                for (int y = m_sy; y <= m_ey; ++y)
                {
                    var index = x + y * m_width;
                    var cell = m_cells[index];
                    var viscosity = m_cellProperties[(int)cell.m_type].viscosity * math.min(1f, cell.m_amount);

                    if (viscosity <= 0.001f)
                        continue;
                    
                    m_output.Add(new(x, y, viscosity));
                }
            }
        }

        public Vector3[] GetOutput()
        {
            var array = m_output.AsArray().Reinterpret<Vector3>();
            var output = new Vector3[m_output.Length];
            NativeArray<Vector3>.Copy(array, output, output.Length);
            m_output.Dispose();
            return output;
        }
    }

    public void AddIntoGrid(Vector2 _position, Cell _cell)
    {
        var i = GetCell(_position, Mathf.RoundToInt);
        var job = new AddIntoGridJob(m_cells, m_fluidCells, m_cellPropertiesNative, _cell, i.x, i.y, m_levelWidth, m_levelHeight);
        job.Schedule().Complete();
        job.Dispose();
    }
    
    [BurstCompile]
    private struct AddIntoGridJob : IJob
    {
        private const int c_Range = 3;
        private NativeArray<Cell> m_cells;
        private NativeHashSet<int2> m_fluidCells;
        [ReadOnly] private NativeArray<CellProperties> m_cellProperties;
        [ReadOnly] private Cell m_cell;
        [ReadOnly] private int m_x, m_y;
        [ReadOnly] private int m_width, m_height;
        private NativeHashSet<int2> m_searchedCells;

        public AddIntoGridJob(NativeArray<Cell> _cells, NativeHashSet<int2> _fluidCells, NativeArray<CellProperties> _properties, Cell _cell, int _x, int _y, int _width, int _height)
        {
            const int d = c_Range * 2 + 1;
            m_searchedCells = new(d * d, Allocator.TempJob);
            
            m_cells = _cells;
            m_fluidCells = _fluidCells;
            m_cellProperties = _properties;
            m_cell = _cell;
            m_x = _x;
            m_y = _y;
            m_width = _width;
            m_height = _height;
        }

        public void Execute()
        {
            CheckCellRecursive(m_x, m_y, c_Range);
        }

        private bool CheckCellRecursive(int _x, int _y, int _range)
        {
            m_searchedCells.Add(new(_x, _y));
            var cell = m_cells[_x + _y * m_width];
            var properties = m_cellProperties[(int)cell.m_type];
            if (properties.viscosity >= 1f)
                return false;

            var wasFluid = cell.IsLiquid(m_cellProperties);
            cell = cell.TryAdd(m_cell, out var success);
            if (success)
            {
                var isFluid = cell.IsLiquid(m_cellProperties);
                if (isFluid && !wasFluid)
                    m_fluidCells.Add(new(_x, _y));
                else if (wasFluid && !isFluid)
                    m_fluidCells.Remove(new(_x, _y));
                
                m_cells[_x + _y * m_width] = cell;
                return true;
            }

            if (_range <= 0) 
                return false;
            
            for (int newX = _x - 1; newX <= _x + 1; ++newX)
            {
                for (int newY = _y - 1; newY <= _y + 1; ++newY)
                {
                    if (newX < 0 || newX >= m_width || newY < 0 || newY >= m_height)
                        continue;
                    if (newX == _x && newY == _y)
                        continue;

                    if (CheckCellRecursive(newX, newY, _range - 1))
                        return true;
                }
            }
            return false;
        }
        
        public void Dispose()
        {
            m_searchedCells.Dispose();
        }
    }

    private void FixedUpdate()
    {
        UpdateFluids();
        //Debug.LogError($"Fluid count: {m_fluidCells.Count}");
    }

    private void UpdateFluids()
    {
        var job = new UpdateFluidsJob(m_cells, m_cellPropertiesNative, m_fluidCells, m_levelWidth, m_levelHeight);
        job.Schedule().Complete();
        job.Dispose();
    }
    
    [BurstCompile]
    private struct UpdateFluidsJob : IJob
    {
        private NativeArray<Cell> m_cells;
        private NativeHashSet<int2> m_fluidCells;
        [ReadOnly] private NativeArray<CellProperties> m_cellProperties;
        [ReadOnly] private int m_width, m_height;
        [ReadOnly] private Random m_random;

        public UpdateFluidsJob(NativeArray<Cell> _cells, NativeArray<CellProperties> _properties, NativeHashSet<int2> _fluidCells, int _width, int _height)
        {
            m_cells = _cells;
            m_cellProperties = _properties;
            m_fluidCells = _fluidCells;
            m_width = _width;
            m_height = _height;
            m_random = Random.CreateFromIndex((uint)UnityEngine.Random.Range(0u, uint.MaxValue));
        }

        public void Execute()
        {
            var fluidCellsCopy = m_fluidCells.ToNativeArray(Allocator.Temp);
            for (int i = 0; i < fluidCellsCopy.Length; ++i)
            {
                var cellIndex = fluidCellsCopy[i];
                var cell = m_cells[cellIndex.x + cellIndex.y * m_width];
                UpdateCell(ref cell, cellIndex);
                if (!cell.IsLiquid(m_cellProperties))
                    m_fluidCells.Remove(cellIndex);
                m_cells[cellIndex.x + cellIndex.y * m_width] = cell;
            }
            fluidCellsCopy.Dispose();
        }

        private void UpdateCell(ref Cell _cell, int2 _index)
        {
            var fullness = _cell.m_amount;

            var belowIndex = _index + new int2(0, -1);
            var belowValid = belowIndex.y >= 0;
            var belowFullness = float.PositiveInfinity;
            if (belowValid)
                belowFullness = CheckFullness(_cell.m_type, belowIndex);

            var leftIndex = _index + new int2(-1, 0);
            var leftValid = leftIndex.x >= 0;
            var leftFullness = float.PositiveInfinity;
            if (leftValid)
                leftFullness = CheckFullness(_cell.m_type, leftIndex);

            var rightIndex = _index + new int2(1, 0);
            var rightValid = rightIndex.x < m_width;
            var rightFullness = float.PositiveInfinity;
            if (rightValid)
                rightFullness = CheckFullness(_cell.m_type, rightIndex);

            var aboveIndex = _index + new int2(0, 1);
            var aboveValid = aboveIndex.y < m_height;
            var aboveFullness = float.PositiveInfinity;
            if (aboveValid)
                aboveFullness = CheckFullness(_cell.m_type, aboveIndex);

            const float minimumFlowThreshold = 0.1f;
            const float lateralDampeningFactor = 0.8f;
            const float upwardDampeningFactor = 0.5f;

            var belowAcceptance = fullness - belowFullness + 1f;
            belowAcceptance = math.min(fullness, belowAcceptance);
            if (belowAcceptance > 0f)
            {
                AddToCell(_cell, belowIndex, belowAcceptance);
                fullness -= belowAcceptance;
            }


            var leftAcceptance = math.max(0f, (fullness - leftFullness) * lateralDampeningFactor);
            var rightAcceptance = math.max(0f, (fullness - rightFullness) * lateralDampeningFactor);
            var lateralAcceptance = leftAcceptance + rightAcceptance;
            var transferTotal = math.min(fullness, lateralAcceptance);
            if (transferTotal > minimumFlowThreshold)
            {
                AddToCell(_cell, leftIndex, leftAcceptance * transferTotal / lateralAcceptance);
                AddToCell(_cell, rightIndex, rightAcceptance * transferTotal / lateralAcceptance);
                fullness -= transferTotal;
            }

            var aboveAcceptance = fullness - aboveFullness - 1f;
            aboveAcceptance = math.min(fullness, aboveAcceptance) * upwardDampeningFactor;
            if (aboveAcceptance > minimumFlowThreshold)
            {
                AddToCell(_cell, aboveIndex, aboveAcceptance);
                fullness -= aboveAcceptance;
            }

            _cell.m_amount = fullness;
            if (fullness <= 0)
                _cell.m_type = Cell.Type.Empty;
        }

        private float CheckFullness(Cell.Type _type, int2 _index)
        {
            var other = m_cells[_index.x + _index.y * m_width];
            if (other.m_type == Cell.Type.Empty)
                return 0f;
            return other.m_type != _type ? float.PositiveInfinity : other.m_amount;
        }

        private void AddToCell(Cell _cell, int2 _index, float _amount)
        {
            if (_amount <= 0f)
                return;
            
            var other = m_cells[_index.x + _index.y * m_width];
            var wasLiquid = other.IsLiquid(m_cellProperties);
            other = other.Add(_cell.m_type, _amount);
            var isLiquid = other.IsLiquid(m_cellProperties);
            
            if (isLiquid && !wasLiquid)
                m_fluidCells.Add(_index);
            m_cells[_index.x + _index.y * m_width] = other;
        }
        
        public void Dispose()
        {
            
        }
    }

    private void OnDestroy()
    {
        m_cells.Dispose();
        m_fluidCells.Dispose();
        m_cellPropertiesNative.Dispose();
    }
}