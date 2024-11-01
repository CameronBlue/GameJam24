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
    [SerializeField] private RenderTexture m_map;
    [SerializeField] private Texture2D m_level;
    private Texture2D m_textureHolder;
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
        m_image.localScale = new Vector3(c_ImageScale, c_ImageScale, 1f);
        m_levelWidth = m_level.width;
        m_levelHeight = m_level.height;
        m_textureHolder = new Texture2D(m_levelWidth, m_levelHeight, TextureFormat.RGBA32, false);
        
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
        var job = new RenderJob(m_cells, m_cellPropertiesNative, m_levelWidth * m_levelHeight);
        job.Schedule(m_levelWidth * m_levelHeight, 64).Complete();
        job.SetTexture(m_textureHolder);
        Graphics.Blit(m_textureHolder, m_map);

        if (Input.GetMouseButtonDown(1))
        {
            var point = (Vector2)Camera.main.ScreenToWorldPoint(Input.mousePosition);
            var index = GetCell(point, Mathf.RoundToInt);
            var cell = m_cells[index.x + index.y * m_levelWidth];
            Debug.LogError($"Clicked on {index} which is {cell.m_type} with {cell.m_amount}");
        }
    }

    [BurstCompile]
    private struct RenderJob : IJobParallelFor
    {
        [ReadOnly] private NativeArray<Cell> m_cells;
        [ReadOnly] private NativeArray<CellProperties> m_cellProperties;
        [NativeDisableParallelForRestriction] private NativeArray<byte> m_texture;

        public RenderJob(NativeArray<Cell> _cells, NativeArray<CellProperties> _properties, int _length)
        {
            m_cells = _cells;
            m_cellProperties = _properties;
            m_texture = new NativeArray<byte>(_length * 4, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        }
        
        public void Execute(int _index)
        {
            var cell = m_cells[_index];
            var colour = m_cellProperties[(int)cell.m_type].colour;
            
            m_texture[4 * _index] = colour.r;
            m_texture[4 * _index + 1] = colour.g;
            m_texture[4 * _index + 2] = colour.b;
            m_texture[4 * _index + 3] = (byte)((cell.m_type == Cell.Type.Empty ? 0f : 1f) * colour.a);
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
    }

    private void UpdateFluids()
    {
        var job = new UpdateFluidsJob(m_cells, m_cellPropertiesNative, m_fluidCells, m_levelWidth, m_levelHeight);
        job.Schedule().Complete();
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

            var belowAcceptance = fullness - belowFullness;
            if (belowAcceptance > 0f)
            {
                AddToCell(_cell, belowIndex, math.min(fullness, belowAcceptance));
                fullness -= belowAcceptance;
            }
            
            var leftAcceptance = fullness - leftFullness;
            var rightAcceptance = fullness - rightFullness;
            if (leftAcceptance > 0f)
            {
                if (rightAcceptance > 0f)
                {
                    var acceptances = new float2(leftAcceptance, rightAcceptance);
                    var tot = math.min(fullness, leftAcceptance + rightAcceptance);
                    acceptances = math.normalize(acceptances) * tot;
                    AddToCell(_cell, leftIndex, acceptances.x);
                    AddToCell(_cell, rightIndex, acceptances.y);
                    fullness -= tot;
                }
                else
                {
                    AddToCell(_cell, leftIndex, math.min(fullness, leftAcceptance));
                    fullness -= leftAcceptance;
                }
            }
            else if (rightAcceptance > 0f)
            {
                AddToCell(_cell, rightIndex, math.min(fullness, rightAcceptance));
                fullness -= rightAcceptance;
            }
            
            var aboveAcceptance = fullness - aboveFullness;
            if (aboveAcceptance > 0f)
            {
                AddToCell(_cell, aboveIndex, math.min(fullness, aboveAcceptance));
                fullness -= aboveAcceptance;
            }

            _cell.m_amount = fullness;
            if (fullness == 0)
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
    }

    private void OnDestroy()
    {
        m_cells.Dispose();
        m_fluidCells.Dispose();
        m_cellPropertiesNative.Dispose();
    }
}