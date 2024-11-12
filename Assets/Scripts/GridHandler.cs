using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using Random = Unity.Mathematics.Random;

public class GridHandler : MonoBehaviour
{
    public static GridHandler Me;
    
    public struct Cell
    {
        private const float c_EmptyThreshold = 1e-10f;
        public static Cell Null => new() {m_type = Type.Null};
        public enum Type
        {
            Empty,
            Wall,
            Water,
            Lava,
            Wood,
            Fire,
            Smoke,
            Platform,
            Acid,
            Gas,
            Slime,
            Bounce,
            Bedrock,
            Null //A real cell should never be this type 
        }
        
        public Type m_type;
        public float m_amount;
        public bool m_crystallised;
        
        public bool IsEmpty => IsType(Type.Empty);
        public bool IsNull => IsType(Type.Null);
        public bool IsType(Type _type) => m_type == _type;
        
        public bool IsFluid(NativeArray<CellProperties> _properties, bool _includeEmpty)
        {
            return (_includeEmpty || !IsEmpty) && !m_crystallised && _properties[(int)m_type].viscosity < 1f;
        }

        public void Add(Type _type, float _amount)
        {
            m_amount = math.select(_amount + m_amount, _amount, IsEmpty);
            m_type = _type;
        }

        public void Remove(float _amount)
        {
            var newAmount = m_amount - _amount;
            m_amount = math.select(0f, newAmount, newAmount > c_EmptyThreshold);
            m_type = (Type)math.select((int)Type.Empty, (int)m_type, newAmount > c_EmptyThreshold);
        }

        public void Clear()
        {
            m_amount = 0f;
            m_type = Type.Empty;
        }

        public void Combust()
        {
            m_type = Type.Fire;
            m_crystallised = false;
        }
    }
    
    [Serializable] public struct CellProperties 
    { 
        [Tooltip("Please don't change this, I just couldn't find a way to lock it")]
        public Cell.Type type; //Only here for inspector
        [Tooltip("How the potion breaks: 0=Square, 1=Perpendicular, 2=Parallel, 3=Parallel-Replace")]
        public int potionStyle;
        [Tooltip("Only used for non-grid particles and potions")]
        public Color32 colour;
        [Tooltip("Only used for potions")] 
        public Color32 colour2;
        [Tooltip("Determines which way gravity applies, and which tiles it will sink through")]
        public float weight;
        [Tooltip("Whether the texture edges will apply only next to empty tiles, or at all edges of this type")]
        public bool edgeNeedsEmpty;
        [Tooltip("How slowly the fluid flows (1 means this block is solid)")]
        public float viscosity;
        [Tooltip("How quickly the fluid corrodes tiles. Also changes how quickly the acid is used up")]
        public float corrosivity;
        [Tooltip("How easily a tile is corroded by acid")]
        public float corrodability;
        [Tooltip("How likely the tile is to alight neighbours. Also changes how quickly the fire burns out")]
        public float heat;
        [Tooltip("How likely a tile is to catch on fire")]
        public float flammability;
        [Tooltip("How likely a tile is to be exploded")]
        public float explodability;
        [Tooltip("How likely a tile is to decay each frame")]
        public float decayRate;
        [Tooltip("Whether this liquid solidifies on contact with solid blocks")]
        public bool crystallisable;
    }
    
    [SerializeField] private List<CellProperties> m_cellProperties;
    private NativeArray<CellProperties> m_cellPropertiesNative;

    [SerializeField] private RectTransform m_image;
    [SerializeField] private RectTransform m_background;
    [SerializeField] private Texture2D m_level;
    [SerializeField] private Texture2D m_textureHolder;
    private int m_levelWidth;
    private int m_levelHeight;
    
    private NativeArray<Cell> m_cells;
    private NativeHashSet<int2> m_fluidCells;
    
    private bool fluidUpdateOngoing;
    private JobHandle fluidUpdateJob;

    private int2 m_spawnPoint;
    private int2 m_endPoint;
    
    private void Awake()
    {
        Me = this;
    }

    public Vector2 GetSpawnPoint()
    {
        return GetPosition(new(m_spawnPoint.x, m_spawnPoint.y));
    }
    
    public Vector2 GetEndPoint()
    {
        return GetPosition(new(m_endPoint.x, m_endPoint.y));
    }
    
    private void Start()
    {
        if (SaveManager.Me)
            m_level = SaveManager.Me.GetLevelTexture();

        SetupImage();
        SetupProperties();
        LoadMap();
    }
    
    private void SetupImage()
    {
        m_levelWidth = m_level.width;
        m_levelHeight = m_level.height;
        m_image.localScale = new Vector3(Manager.c_ImageScale, Manager.c_ImageScale, 1f);
        m_background.localScale = new Vector3(Manager.c_ImageScale, Manager.c_ImageScale, 1f);
        m_image.sizeDelta = new Vector2(m_levelWidth, m_levelHeight);
        m_background.sizeDelta = new Vector2(m_levelWidth, m_levelHeight);
        
        m_textureHolder = new Texture2D(m_levelWidth, m_levelHeight, TextureFormat.RGBA32, 0, true)
        {
            filterMode = FilterMode.Point
        };
        m_image.GetComponent<RawImage>().material.SetTexture("_MainTex", m_textureHolder);
    }

    private void SetupProperties()
    {
        m_cellPropertiesNative = new(m_cellProperties.Count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        for (int i = 0; i < m_cellProperties.Count; ++i) //I will optimise if needed but should be a short array
            m_cellPropertiesNative[i] = m_cellProperties[i];
    }

    private void LoadMap()
    {
        m_cells = new NativeArray<Cell>(m_levelWidth * m_levelHeight + 1, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        m_cells[^1] = Cell.Null;
        m_fluidCells = new NativeHashSet<int2>(m_levelWidth * m_levelHeight, Allocator.Persistent); //It should never come close to this amount but if something goes wrong it will prevent a memory leak
        
        var fillMapJob = new FillMapJob(m_cells, m_level);
        fillMapJob.Schedule(m_levelWidth * m_levelHeight, 64).Complete();
        fillMapJob.GetSpecialCells(out var specialCells);
        m_spawnPoint = specialCells[0];
        m_endPoint = specialCells[1];
        
        var findFluidsJob = new FindFluidsJob(m_cells, m_cellPropertiesNative, m_fluidCells, m_levelWidth, m_levelHeight);
        findFluidsJob.Schedule().Complete();
    }
    
    [BurstCompile]
    private struct FillMapJob : IJobParallelFor
    {
        private NativeArray<Cell> m_cells;
        [NativeDisableParallelForRestriction] private NativeArray<int2> m_specialCells;
        [ReadOnly] private int m_width, m_height;
        [ReadOnly] private NativeArray<byte> m_texture;

        public FillMapJob(NativeArray<Cell> _cells, Texture2D _image)
        {
            m_cells = _cells;
            m_texture = _image.GetRawTextureData<byte>();
            m_width = _image.width;
            m_height = _image.height;
            m_specialCells = new NativeArray<int2>(2, Allocator.TempJob);
        }
        
        public void Execute(int _index)
        {
            var r = m_texture[4 * _index];
            var g = m_texture[4 * _index + 1];
            var b = m_texture[4 * _index + 2];
            var a = m_texture[4 * _index + 3];

            if (r == 255 && g == 255)
            {
                m_specialCells[1 - (b / 255)] = new(_index % m_width, _index / m_width);
                m_cells[_index] = new Cell { m_type = Cell.Type.Empty, m_amount = 0f };
                return;
            }
            var type = (r / 64) * 16 + (g / 64) * 4 + (b / 64);
            m_cells[_index] = new Cell { m_type = (Cell.Type)type, m_amount = (a + 1) * 0.00390625f }; // 1/256
        }
        
        public void GetSpecialCells(out int2[] _output)
        {
            _output = new int2[m_specialCells.Length];
            NativeArray<int2>.Copy(m_specialCells, _output, _output.Length);
            m_specialCells.Dispose();
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
                    if (cell.IsFluid(m_cellProperties, false))
                        m_fluidCells.Add(new(i, j));
                }
            }
        }
    }

    public Vector2 DebugPoint(Vector2 _point)
    {
        var index = GetCell(_point, Mathf.RoundToInt);
        if (Input.GetMouseButtonDown(1))
        {
            var cell = m_cells[index.x + index.y * m_levelWidth];
            Debug.Log($"Clicked on {index} which is {cell.m_type} with {cell.m_amount}. Is registered: {m_fluidCells.Contains(index)}");
        }
        return GetPosition(new(index.x, index.y));
    }

    public void RenderTiles()
    {
        var job = new RenderJob(m_cells, m_cellPropertiesNative, m_levelWidth, m_levelHeight);
        job.Schedule(m_levelWidth * m_levelHeight, 64).Complete();
        job.SetTexture(m_textureHolder);
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
            var opacity = math.min(1f, cell.m_amount);
            
            m_texture[4 * _index] = (byte)type;
            m_texture[4 * _index + 1] = GetNeighbourStatus(cell.m_type, _index, !properties.edgeNeedsEmpty);
            m_texture[4 * _index + 2] = (byte)(255 * properties.viscosity);
            m_texture[4 * _index + 3] = (byte)(opacity * properties.colour.a);
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
    
    public CellProperties GetProperties(Cell.Type _type)
    {
        return m_cellProperties[(int)_type];
    }
    
    public int2 GetCell(Vector2 _p, Func<float, int> Clamp)
    {
        var x = Mathf.Clamp(Clamp(m_levelWidth * 0.5f + _p.x / Manager.c_CellDiameter), 0, m_levelWidth - 1);
        var y = Mathf.Clamp(Clamp(m_levelHeight * 0.5f + _p.y / Manager.c_CellDiameter), 0, m_levelHeight - 1);
        return new(x, y);
    }

    public float2 GetCellFloat(Vector2 _p)
    {
        var x = Mathf.Clamp(m_levelWidth * 0.5f + _p.x / Manager.c_CellDiameter, 0, m_levelWidth - 1);
        var y = Mathf.Clamp(m_levelHeight * 0.5f + _p.y / Manager.c_CellDiameter, 0, m_levelHeight - 1);
        return new(x, y);
    }
    
    public float2 GetPosition(int2 _i) => GetPosition(_i, new(m_levelWidth, m_levelHeight));
    
    public static float2 GetPosition(int2 _i, int2 _l)
    {
        _i.x -= _l.x >> 1;
        _i.y -= _l.y >> 1;
        return (float2)_i * Manager.c_CellDiameter;
    }

    public Vector2 GetPositionFloat(float2 _cellPos)
    {
        _cellPos.x -= m_levelWidth * 0.5f;
        _cellPos.y -= m_levelHeight * 0.5f;
        return _cellPos * Manager.c_CellDiameter;
    }

    public void CheckCells(ref ColliderData[] _bounds)
    {
        var job = new CheckCellsJob(m_cells, m_cellPropertiesNative, _bounds, m_levelWidth, m_levelHeight);
        job.Schedule(_bounds.Length, 1).Complete();
        job.Finish(ref _bounds);
    }

    public struct ColliderData
    {
        public float2 m_min;
        public float2 m_max;
        public float2 m_prevMin;
        public float2 m_prevMax;
        public float2 m_velocity;

        public int4 m_blocksAround;
        public int4 m_bounceAround;
        public int4 m_slimeAround;
        
        public ColliderData(float2 _min, float2 _max, float2 _prevMin, float2 _prevMax, float2 _velocity)
        {
            m_min = _min;
            m_max = _max;
            m_prevMin = _prevMin;
            m_prevMax = _prevMax;
            m_velocity = _velocity;
            m_blocksAround = 0;
            m_bounceAround = 0;
            m_slimeAround = 0;
        }
        
        public float2 GetCentre => (m_min + m_max) * 0.5f;

        public void ResolveIntersection(int2 _prevRelative, float2 _cellMin, float2 _cellMax)
        {
            float2 offset;
            if (_prevRelative.x == 0)
                offset = new float2(0f, _prevRelative.y == 1 ? _cellMax.y - m_min.y : _cellMin.y - m_max.y);
            else if (_prevRelative.y == 0)
                offset = new float2(_prevRelative.x == 1 ? _cellMax.x - m_min.x : _cellMin.x - m_max.x, 0f);
            else
            {
                offset = m_velocity.y * m_velocity.y > m_velocity.x * m_velocity.x
                    ? new float2(_prevRelative.x == 1 ? _cellMax.x - m_min.x : _cellMin.x - m_max.x, 0f)
                    : new float2(0f, _prevRelative.y == 1 ? _cellMax.y - m_min.y : _cellMin.y - m_max.y);
            }
            if (math.lengthsq(offset) < 0.001f * 0.001f)
                return;
            
            m_min += offset;
            m_max += offset;
            m_velocity -= offset * math.dot(m_velocity, offset) / math.dot(offset, offset);
        }
    }

    [BurstCompile]
    private struct CheckCellsJob : IJobParallelFor
    {
        [ReadOnly] private NativeArray<Cell> m_cells;
        [ReadOnly] private NativeArray<CellProperties> m_cellProperties;
        [ReadOnly] private int2 m_mapSize;
        private NativeArray<ColliderData> m_bounds;

        public CheckCellsJob(NativeArray<Cell> _cells, NativeArray<CellProperties> _properties, ColliderData[] _bounds, int _width, int _height)
        {
            m_cells = _cells;
            m_cellProperties = _properties;
            m_mapSize = new(_width, _height);
            m_bounds = new(_bounds, Allocator.TempJob);
        }

        public void Execute(int _index)
        {
            ref ColliderData bounds = ref m_bounds.RefAt(_index);
            ResolveIntersections(ref bounds);
            SearchAround(ref bounds);
        }

        private void ResolveIntersections(ref ColliderData _bounds)
        {
            var max = (int2)math.ceil(_bounds.m_max);
            var min = (int2)math.floor(_bounds.m_min);
            
            NativeList<float4> ambiguousIntersections = new(Allocator.Temp);
            for (int x = min.x; x < max.x; ++x)
            {
                for (int y = min.y; y < max.y; ++y)
                {
                    var index = x + y * m_mapSize.x;
                    var cell = m_cells[index];
                    if (cell.IsFluid(m_cellProperties, true))
                        continue;
                    
                    var cellPos = new float2(x, y);
                    var cellMin = cellPos;
                    var cellMax = cellPos + 1f;
                    var rel = CompareBounds(_bounds.m_min, _bounds.m_max, cellMin, cellMax);
                    if (rel.x != 0 || rel.y != 0)
                        continue;
                    
                    var prevRel = CompareBounds(_bounds.m_prevMin, _bounds.m_prevMax, cellMin, cellMax);
                    if (prevRel.x == 0 && prevRel.y == 0)
                        continue; //Alas we are unable to unstuck
                    
                    if (prevRel.x != 0 && prevRel.y != 0)
                    {
                        ambiguousIntersections.Add(new(cellMin, cellMax));
                        continue;
                    }

                    _bounds.ResolveIntersection(prevRel, cellMin, cellMax);
                }
            }
            foreach (var intersection in ambiguousIntersections)
            {
                var cellMin = intersection.xy;
                var cellMax = intersection.zw;
                var rel = CompareBounds(_bounds.m_min, _bounds.m_max, cellMin, cellMax);
                if (rel.x != 0 && rel.y != 0)
                    continue;
                
                var prevRel = CompareBounds(_bounds.m_prevMin, _bounds.m_prevMax, cellMin, cellMax);
                _bounds.ResolveIntersection(prevRel, cellMin, cellMax);
            }
            
            ambiguousIntersections.Dispose();
        }
        
        private void SearchAround(ref ColliderData _bounds)
        {
            var max = (int2)math.ceil(_bounds.m_max);
            var min = (int2)math.floor(_bounds.m_min);

            var left = min.x - 1;
            if (left >= 0)
            {
                for (int y = min.y; y < max.y; ++y)
                {
                    var index = left + y * m_mapSize.x;
                    var cell = m_cells[index];
                    if (cell.IsType(Cell.Type.Bounce))
                        _bounds.m_bounceAround.x++;
                    else if (cell.IsType(Cell.Type.Slime))
                        _bounds.m_slimeAround.x++;
                    else if (!cell.IsFluid(m_cellProperties, true))
                        _bounds.m_blocksAround.x++;
                }
            }

            var bottom = min.y - 1;
            if (bottom >= 0)
            {
                for (int x = min.x; x < max.x; ++x)
                {
                    var index = x + bottom * m_mapSize.x;
                    var cell = m_cells[index];
                    if (cell.IsType(Cell.Type.Bounce))
                        _bounds.m_bounceAround.y++;
                    else if (cell.IsType(Cell.Type.Slime))
                        _bounds.m_slimeAround.y++;
                    else if (!cell.IsFluid(m_cellProperties, true))
                        _bounds.m_blocksAround.y++;
                }
            }
            var right = max.x;
            if (right < m_mapSize.x)
            {
                for (int y = min.y; y < max.y; ++y)
                {
                    var index = right + y * m_mapSize.x;
                    var cell = m_cells[index];
                    if (cell.IsType(Cell.Type.Bounce))
                        _bounds.m_bounceAround.z++;
                    else if (cell.IsType(Cell.Type.Slime))
                        _bounds.m_slimeAround.z++;
                    else if (!cell.IsFluid(m_cellProperties, true))
                        _bounds.m_blocksAround.z++;
                }
            }
            var top = max.y;
            if (top < m_mapSize.y)
            {
                for (int x = min.x; x < max.x; ++x)
                {
                    var index = x + top * m_mapSize.x;
                    var cell = m_cells[index];
                    if (cell.IsType(Cell.Type.Bounce))
                        _bounds.m_bounceAround.w++;
                    else if (cell.IsType(Cell.Type.Slime))
                        _bounds.m_slimeAround.w++;
                    else if (!cell.IsFluid(m_cellProperties, true))
                        _bounds.m_blocksAround.w++;
                }
            }
        }

        private int2 CompareBounds(float2 _minA, float2 _maxA, float2 _minB, float2 _maxB)
        {
            int x = (_minA.x >= _maxB.x) ? 1 : ((_maxA.x <= _minB.x) ? -1 : 0);
            int y = (_minA.y >= _maxB.y) ? 1 : ((_maxA.y <= _minB.y) ? -1 : 0);
            return new(x, y);
        }

        public void Finish(ref ColliderData[] _fillInto)
        {
            _fillInto = new ColliderData[m_bounds.Length];
            NativeArray<ColliderData>.Copy(m_bounds, _fillInto, _fillInto.Length);
            m_bounds.Dispose();
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
        private const int c_Range = 5;
        private NativeArray<Cell> m_cells;
        private NativeHashSet<int2> m_fluidCells;
        [ReadOnly] private NativeArray<CellProperties> m_cellProperties;
        [ReadOnly] private Cell m_cell;
        [ReadOnly] private int m_x, m_y;
        [ReadOnly] private int m_width, m_height;
        [ReadOnly] private bool m_replaceMode;
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
            m_replaceMode = _properties[(int)_cell.m_type].potionStyle == 3;
        }

        public void Execute()
        {
            CheckCellRecursive(new(m_x, m_y), c_Range);
        }

        private bool CheckCellRecursive(int2 _pos, int _range)
        {
            m_searchedCells.Add(_pos);
            ref Cell cell = ref m_cells.RefAt(_pos.x + _pos.y * m_width);
            var canAdd = m_replaceMode ? !cell.IsEmpty && !cell.IsNull && !cell.IsType(m_cell.m_type) : cell.IsEmpty || cell.IsType(m_cell.m_type);
            var hasRoom = m_replaceMode || cell.IsFluid(m_cellProperties, true) || cell.m_amount < 1f;
            if (canAdd && hasRoom)
            {
                var wasFluid = cell.IsFluid(m_cellProperties, false);
                if (m_replaceMode)
                    cell = m_cell;
                else
                    cell.Add(m_cell.m_type, m_cell.m_amount);
                var isFluid = cell.IsFluid(m_cellProperties, false);
                if (isFluid && !wasFluid)
                    m_fluidCells.Add(_pos);
                return true;
            }
            if (_range <= 0 || !canAdd)
                return false;
            
            for (int newX = _pos.x - 1; newX <= _pos.x + 1; ++newX)
            {
                for (int newY = _pos.y - 1; newY <= _pos.y + 1; ++newY)
                {
                    if (newX < 0 || newX >= m_width || newY < 0 || newY >= m_height)
                        continue;
                    var newPos = new int2(newX, newY);
                    if (math.all(newPos == _pos))
                        continue;

                    if (CheckCellRecursive(newPos, _range - 1))
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
    
    public void Explode(Vector2 _position, int _radius)
    {
        CompleteFluidUpdate();
        
        var i = GetCell(_position, Mathf.RoundToInt);
        var job = new ExplodeJob(m_cells, m_fluidCells, m_cellPropertiesNative, i, m_levelWidth, m_levelHeight, _radius);
        job.Schedule().Complete();
    }

    [BurstCompile]
    private struct ExplodeJob : IJob
    {
        private NativeArray<Cell> m_cells;
        private NativeHashSet<int2> m_fluidCells;
        [ReadOnly] private NativeArray<CellProperties> m_cellProperties;
        [ReadOnly] private int2 m_epicenter;
        [ReadOnly] private int2 m_levelSize;
        [ReadOnly] private int m_radius;
        [ReadOnly] private Random m_random;
        
        public ExplodeJob(NativeArray<Cell> _cells, NativeHashSet<int2> _fluidCells, NativeArray<CellProperties> _properties, int2 _epicenter, int _width, int _height, int _radius)
        {
            m_cells = _cells;
            m_fluidCells = _fluidCells;
            m_cellProperties = _properties;
            m_epicenter = _epicenter;
            m_levelSize = new(_width, _height);
            m_radius = _radius;
            m_random = new Random((uint)UnityEngine.Random.Range(0, uint.MaxValue));
        }
        
        public void Execute()
        {
            var min = math.max(0, m_epicenter - m_radius);
            var max = math.min(m_epicenter + m_radius, m_levelSize);

            for (int x = min.x; x < max.x; ++x)
            {
                for (int y = min.y; y < max.y; ++y)
                {
                    ref Cell cell = ref m_cells.RefAt(x + y * m_levelSize.x);
                    if (cell.IsFluid(m_cellProperties, true) || m_cellProperties[(int)cell.m_type].explodability <= 0f)
                        continue;
                    
                    var sqrDist = math.lengthsq(m_epicenter - new int2(x, y)) + 1;
                    var fractionAway = sqrDist / (m_radius * m_radius);
                    if (fractionAway > 1f)
                        continue;
                    
                    if (m_random.NextFloat(0f, 1f) > 5 * (1 - fractionAway)) 
                        continue;
                    
                    cell.m_type = Cell.Type.Gas;
                    cell.m_amount *= 5f;
                    cell.m_crystallised = false;
                    m_fluidCells.Add(new(x, y));
                }
            }
        }
    }
    
    public void UpdateFluids()
    {
        var job = new UpdateFluidsJob(m_cells, m_cellPropertiesNative, m_fluidCells, m_levelWidth, m_levelHeight);
        fluidUpdateJob = job.Schedule();
        fluidUpdateOngoing = true;
    }
    
    public void CompleteFluidUpdate()
    {
        if (!fluidUpdateOngoing)
            return;
        fluidUpdateJob.Complete();
        fluidUpdateOngoing = false;
    }
    
    [BurstCompile]
    private struct UpdateFluidsJob : IJob
    {
        const float c_MinimumFlowThreshold = 0.01f;
        const float c_LateralDampeningFactor = 0.5f;
        
        private NativeArray<Cell> m_cells;
        private NativeHashSet<int2> m_fluidCells;
        [ReadOnly] private NativeArray<CellProperties> m_cellProperties;
        [ReadOnly] private int m_width, m_height;
        [ReadOnly] private float m_fixedDeltaTime;
        [ReadOnly] private Random m_random;

        public UpdateFluidsJob(NativeArray<Cell> _cells, NativeArray<CellProperties> _properties, NativeHashSet<int2> _fluidCells, int _width, int _height)
        {
            m_cells = _cells;
            m_cellProperties = _properties;
            m_fluidCells = _fluidCells;
            m_width = _width;
            m_height = _height;
            m_fixedDeltaTime = Time.fixedDeltaTime;
            m_random = new Random((uint)UnityEngine.Random.Range(0, uint.MaxValue));
        }

        private const int c_Cycles = 1;
        private struct HeightComparer : IComparer<int2> { public int Compare(int2 _a, int2 _b) => _a.y.CompareTo(_b.y); }
        public void Execute()
        {
            for (int cycle = 0; cycle < c_Cycles; ++cycle)
            {
                var fluidCellsCopy = m_fluidCells.ToNativeArray(Allocator.Temp);
                fluidCellsCopy.Sort(new HeightComparer());
                foreach (var cell in fluidCellsCopy)
                    UpdateCell(cell);
                fluidCellsCopy.Dispose();
            }
        }

        private void UpdateCell(int2 _index)
        {
            ref Cell cell = ref GetCell(_index);
            var belowIndex = _index + new int2(0, -1);
            var leftIndex = _index + new int2(-1, 0);
            var rightIndex = _index + new int2(1, 0);
            var aboveIndex = _index + new int2(0, 1);
            ref Cell belowCell = ref GetCellChecked(belowIndex);
            ref Cell leftCell = ref GetCellChecked(leftIndex);
            ref Cell rightCell = ref GetCellChecked(rightIndex);
            ref Cell aboveCell = ref GetCellChecked(aboveIndex);

            ActuallyUpdateCell(ref cell, ref belowCell, ref leftCell, ref rightCell, ref aboveCell);
            
            if (!cell.IsFluid(m_cellProperties, false))
                m_fluidCells.Remove(_index);
            UpdateFluidList(ref belowCell, belowIndex);
            UpdateFluidList(ref leftCell, leftIndex);
            UpdateFluidList(ref rightCell, rightIndex);
            UpdateFluidList(ref aboveCell, aboveIndex);
        }

        private void UpdateFluidList(ref Cell _cell, int2 _index)
        {
            if (_cell.IsNull)
                return;

            if (_cell.IsFluid(m_cellProperties, false))
                m_fluidCells.Add(_index);
            else
                m_fluidCells.Remove(_index);
        }
        
        private void ActuallyUpdateCell(ref Cell _cell, ref Cell _below, ref Cell _left, ref Cell _right, ref Cell _above)
        {
            UpdateSlime(ref _cell, ref _below, ref _left, ref _right, ref _above);
            UpdateDecay(ref _cell);
            UpdateBurning(ref _cell, ref _below, ref _left, ref _right, ref _above);
            UpdateCorrosion(ref _cell, ref _below, ref _left, ref _right, ref _above);
            UpdateCrystallisation(ref _cell, ref _below, ref _left, ref _right, ref _above);
            
            var properties = m_cellProperties[(int)_cell.m_type];
            var updateChance = properties.viscosity;
            var rand = m_random.NextFloat(0f, 1f);
            if (rand * rand < updateChance)
                return;
            
            var weight = properties.weight;
            if (weight != 0f)
            {
                UpdateMainDir(ref _cell, ref ((weight > 0f) ? ref _below : ref _above));
                UpdateAuxilliaryDirs(ref _cell, ref _left, ref _right);
            }
            DistributeOverflow(ref _cell, ref _below, ref _left, ref _right, ref _above);
        }

        private void UpdateSlime(ref Cell _cell, ref Cell _below, ref Cell _left, ref Cell _right, ref Cell _above)
        {
            if (!_cell.IsType(Cell.Type.Slime))
                return;

            _cell.m_crystallised = true;

            var aboveIsAir = _above.IsEmpty;
            var belowIsAir = _below.IsEmpty;
            var leftIsAir = _left.IsEmpty;
            var rightIsAir = _right.IsEmpty;
            var touchingAir = aboveIsAir || belowIsAir || leftIsAir || rightIsAir;
            if (!touchingAir)
                return;
            
            if (!_left.IsNull && !leftIsAir)
                _left.m_type = Cell.Type.Slime;
            if (!_right.IsNull && !rightIsAir)
                _right.m_type = Cell.Type.Slime;
            if (!_above.IsNull && !aboveIsAir)
                _above.m_type = Cell.Type.Slime;
            if (!_below.IsNull && !belowIsAir)
                _below.m_type = Cell.Type.Slime;
        }

        private void UpdateDecay(ref Cell _cell)
        {
            var decayRate = m_cellProperties[(int)_cell.m_type].decayRate;
            if (decayRate <= 0f)
                return;
            
            var rand = m_random.NextFloat(0f, 1f);
            if (rand < decayRate)
                _cell.Clear();
        }
        
        private void UpdateBurning(ref Cell _cell, ref Cell _below, ref Cell _left, ref Cell _right, ref Cell _above)
        {
            var properties = m_cellProperties[(int)_cell.m_type];
            float burnPower = properties.heat * m_fixedDeltaTime;
            if (burnPower <= 0f)
                return;
            int airCount = 0;
            if (!_below.IsNull)
            {
                var p = m_cellProperties[(int)_below.m_type];
                if (p.flammability > 0f && m_random.NextFloat(0f, 1f) < p.flammability * burnPower)
                    _below.Combust();
                if (_below.IsEmpty)
                    ++airCount;
            }
            if (!_left.IsNull)
            {
                var p = m_cellProperties[(int)_left.m_type];
                if (p.flammability > 0f && m_random.NextFloat(0f, 1f) < p.flammability * burnPower)
                    _left.Combust();
                if (_left.IsEmpty)
                    ++airCount;
            }
            if (!_right.IsNull)
            {
                var p = m_cellProperties[(int)_right.m_type];
                if (p.flammability > 0f && m_random.NextFloat(0f, 1f) < p.flammability * burnPower)
                    _right.Combust();
                if (_right.IsEmpty)
                    ++airCount;
            }
            if (!_above.IsNull)
            {
                var p = m_cellProperties[(int)_above.m_type];
                if (p.flammability > 0f && m_random.NextFloat(0f, 1f) < p.flammability * burnPower)
                    _above.Combust();
                if (_above.IsEmpty)
                    ++airCount;
            }
            if (m_random.NextFloat(0f, 1f) > burnPower * (airCount + 1))
                return;
            _cell.Clear();
            if (_above.IsEmpty)
                _above.Add(Cell.Type.Smoke, 5f);
        }

        private void UpdateCorrosion(ref Cell _cell, ref Cell _below, ref Cell _left, ref Cell _right, ref Cell _above)
        {
            var properties = m_cellProperties[(int)_cell.m_type];
            float corrosionPower = properties.corrosivity * _cell.m_amount * m_fixedDeltaTime;
            if (corrosionPower <= 0f)
                return;
            int countCorroded = 0;
            if (!_below.IsNull)
            {
                var p = m_cellProperties[(int)_below.m_type];
                if (p.corrodability > 0f)
                {
                    _below.Remove(p.corrodability * corrosionPower);
                    countCorroded++;
                }
            }
            if (!_left.IsNull)
            {
                var p = m_cellProperties[(int)_left.m_type];
                if (p.corrodability > 0f)
                {
                    _left.Remove(p.corrodability * corrosionPower);
                    countCorroded++;
                }
            }
            if (!_right.IsNull)
            {
                var p = m_cellProperties[(int)_right.m_type];
                if (p.corrodability > 0f)
                {
                    _right.Remove(p.corrodability * corrosionPower);
                    countCorroded++;
                }
            }
            if (!_above.IsNull)
            {
                var p = m_cellProperties[(int)_above.m_type];
                if (p.corrodability > 0f)
                {
                    _above.Remove(p.corrodability * corrosionPower);
                    countCorroded++;
                }
            }
            if (countCorroded > 0)
                _cell.Remove(corrosionPower * countCorroded);
        }
        
        private void UpdateCrystallisation(ref Cell _cell, ref Cell _below, ref Cell _left, ref Cell _right, ref Cell _above)
        {
            var properties = m_cellProperties[(int)_cell.m_type];
            if (!properties.crystallisable)
                return;
            
            if (!_below.IsNull && !_below.IsFluid(m_cellProperties, true))
            {
                Crystallise(ref _cell);
                return;
            }
            if (!_left.IsNull && !_left.IsFluid(m_cellProperties, true))
            {
                Crystallise(ref _cell);
                return;
            }
            if (!_right.IsNull && !_right.IsFluid(m_cellProperties, true))
            {
                Crystallise(ref _cell);
                return;
            }
            //No crystallising on undersides for now
        }
        
        private void Crystallise(ref Cell _cell)
        {
            const float c_CrystalliseThreshold = 0.2f;
            
            if (_cell.m_amount > c_CrystalliseThreshold)
            {
                _cell.m_crystallised = true;
                _cell.m_amount = 1f;
                return;
            }
            _cell.Clear();
        }

        private void UpdateMainDir(ref Cell _cell, ref Cell _other)
        {
            var amount = _cell.m_amount;
            if (amount <= 0f)
                return;
            var space = 1f - GetFullness(_cell.m_type, _other, out var canSwap);
            if (canSwap)
            {
                (_cell, _other) = (_other, _cell);
                return;
            }
            if (space <= 0f)
                return;
            Transfer(ref _cell, ref _other, math.min(space, amount));
        }
        
        private void UpdateAuxilliaryDirs(ref Cell _cell, ref Cell _dir1, ref Cell _dir2)
        {
            var amount = _cell.m_amount;
            if (amount <= 0f)
                return;
            var cappedFullness = math.min(1f, amount);
            var type = _cell.m_type;
            int factors = 0;
            float pull1 = 0f, pull2 = 0f;
            if (!_dir1.IsNull)
            {
                var otherFullness1 = GetFullness(type, _dir1, out _);
                pull1 = (cappedFullness - otherFullness1) * c_LateralDampeningFactor;
                if (pull1 > c_MinimumFlowThreshold)
                    factors++;
            }
            if (!_dir2.IsNull)
            {
                var otherFullness2 = GetFullness(type, _dir2, out _);
                pull2 = (cappedFullness - otherFullness2) * c_LateralDampeningFactor;
                if (pull2 > c_MinimumFlowThreshold)
                    factors++;
            }
            if (factors == 0)
                return;
            if (pull1 > c_MinimumFlowThreshold)
                Transfer(ref _cell, ref _dir1, pull1 / factors);
            if (pull2 > c_MinimumFlowThreshold)
                Transfer(ref _cell, ref _dir2, pull2 / factors);
        }
        
        private void DistributeOverflow(ref Cell _cell, ref Cell _below, ref Cell _left, ref Cell _right, ref Cell _above)
        {
            float overFullness = _cell.m_amount - 1f;
            if (overFullness <= 0f)
                return;
            var type = _cell.m_type;
            int factors = 0;
            float downPull = 0f, leftPull = 0f, rightPull = 0f, upPull = 0f;
            if (!_below.IsNull)
            {
                var belowOverfullness = math.max(0f, GetFullness(type, _below, out _) - 1f);
                downPull = (overFullness - belowOverfullness) * 0.5f;
                if (downPull > 0f)
                    factors++;
            }
            if (!_left.IsNull)
            {
                var leftOverfullness = math.max(0f, GetFullness(type, _left, out _) - 1f);
                leftPull = (overFullness - leftOverfullness) * 0.5f;
                if (leftPull > 0f)
                    factors++;
            }
            if (!_right.IsNull)
            {
                var rightOverfullness = math.max(0f, GetFullness(type, _right, out _) - 1f);
                rightPull = (overFullness - rightOverfullness) * 0.5f;
                if (rightPull > 0f)
                    factors++;
            }
            if (!_above.IsNull)
            {
                var aboveOverfullness = math.max(0f, GetFullness(type, _above, out _) - 1f);
                upPull = (overFullness - aboveOverfullness) * 0.5f;
                if (upPull > 0f)
                    factors++;
            }
            if (factors == 0)
                return;
            if (downPull > 0f)
                Transfer(ref _cell, ref _below, downPull / factors);
            if (leftPull > 0f)
                Transfer(ref _cell, ref _left, leftPull / factors);
            if (rightPull > 0f)
                Transfer(ref _cell, ref _right, rightPull / factors);
            if (upPull > 0f)
                Transfer(ref _cell, ref _above, upPull / factors);
        }
        
        private ref Cell GetCell(int2 _index) => ref m_cells.RefAt(_index.x + _index.y * m_width);

        private ref Cell GetCellChecked (int2 _index)
        {
            var indexValid = _index.x >= 0 && _index.x < m_width && _index.y >= 0 && _index.y < m_height;
            return ref m_cells.RefAt(indexValid ? _index.x + _index.y * m_width : m_cells.Length - 1); //Last cell is null
        }

        private float GetFullness(Cell.Type _type, Cell _other, out bool _canSwap)
        {
            _canSwap = !_other.IsNull && _other.IsFluid(m_cellProperties, true) && IsHeavier(_type, _other.m_type);
            if (_other.m_type == _type)
                return _other.m_amount;
            return _other.m_type == Cell.Type.Empty ? 0f : float.PositiveInfinity;
        }
        
        private bool IsHeavier(Cell.Type _a, Cell.Type _b) => m_cellProperties[(int)_a].weight > m_cellProperties[(int)_b].weight;
            

        private void Transfer(ref Cell _cellFrom, ref Cell _cellTo, float _amount)
        {
            _cellTo.Add(_cellFrom.m_type, _amount);
            _cellFrom.Remove(_amount);
        }
    }

    private void OnDestroy()
    {
        m_cells.Dispose();
        m_fluidCells.Dispose();
        m_cellPropertiesNative.Dispose();
    }
}