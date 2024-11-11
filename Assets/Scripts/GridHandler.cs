using System;
using System.Collections.Generic;
using System.Linq;
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
    
    private void Awake()
    {
        Me = this;
    }

    public Vector2 GetSpawnPoint()
    {
        return GetPosition(new(m_spawnPoint.x, m_spawnPoint.y));
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
        //fillMapJob.Schedule(m_levelWidth * m_levelHeight, 64).Complete();
        for ( int i = 0; i < m_levelWidth * m_levelHeight; ++i)
            fillMapJob.Execute(i);
        fillMapJob.GetSpecialCells(out var specialCells);
        m_spawnPoint = specialCells[0];
        
        var findFluidsJob = new FindFluidsJob(m_cells, m_cellPropertiesNative, m_fluidCells, m_levelWidth, m_levelHeight);
        findFluidsJob.Schedule().Complete();
        //findFluidsJob.Execute();
    }
    
    //[BurstCompile]
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
            m_specialCells = new NativeArray<int2>(1, Allocator.TempJob);
        }
        
        public void Execute(int _index)
        {
            var r = m_texture[4 * _index];
            var g = m_texture[4 * _index + 1];
            var b = m_texture[4 * _index + 2];
            var a = m_texture[4 * _index + 3];

            if (r == 255 && g == 255 && b == 255)
            {
                m_specialCells[0] = new(_index % m_width, _index / m_width);
                m_cells[_index] = new Cell { m_type = Cell.Type.Empty, m_amount = 0f };
                return;
            }
            var type = (r / 64) * 16 + (g / 64) * 4 + (b / 64);
            if (type >= (int)Cell.Type.Null)
                type = (int)Cell.Type.Empty;
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

    public byte[] GetTextureData()
    {
        return m_textureHolder.EncodeToPNG();
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
    
    public float2 GetPosition(int2 _i) => GetPosition(_i, new(m_levelWidth, m_levelHeight));
    
    public static float2 GetPosition(int2 _i, int2 _l)
    {
        _i.x -= _l.x >> 1;
        _i.y -= _l.y >> 1;
        return (float2)_i * Manager.c_CellDiameter;
    }

    public Vector3[][] CheckCells(int4[] _bounds)
    {
        var job = new CheckCellsJob(m_cells, m_cellPropertiesNative, _bounds, m_levelWidth, m_levelHeight);
        job.Schedule().Complete();
        return job.GetOutput();
    }


    [BurstCompile]
    private struct CheckCellsJob : IJob
    {
        [ReadOnly] private NativeArray<Cell> m_cells;
        [ReadOnly] private NativeArray<CellProperties> m_cellProperties;
        [ReadOnly] private NativeArray<int4> m_boundsToCheck;
        [ReadOnly] private int2 m_mapSize;
        private NativeList<float3> m_output;
        private NativeArray<int> m_outputAccessIndices; 

        public CheckCellsJob(NativeArray<Cell> _cells, NativeArray<CellProperties> _properties, int4[] _boundsToCheck, int _width, int _height)
        {
            m_cells = _cells;
            m_cellProperties = _properties;
            m_boundsToCheck = new(_boundsToCheck, Allocator.TempJob);
            m_mapSize = new(_width, _height);
            m_output = new(128, Allocator.TempJob);
            m_outputAccessIndices = new(m_boundsToCheck.Length, Allocator.TempJob);
        }

        public void Execute()
        {
            for (int i = 0; i < m_boundsToCheck.Length; ++i)
            {
                var bounds = m_boundsToCheck[i];
                
                for (int x = bounds.x; x <= bounds.z; ++x)
                {
                    for (int y = bounds.y; y <= bounds.w; ++y)
                    {
                        var index = x + y * m_mapSize.x;
                        var cell = m_cells[index];
                        var viscosity = cell.IsFluid(m_cellProperties, true) ? m_cellProperties[(int)cell.m_type].viscosity : 1f;
                        if (viscosity <= 0f)
                            continue;
                        m_output.Add(new(GetPosition(new(x, y), m_mapSize), viscosity));
                    }
                }
                m_outputAccessIndices[i] = m_output.Length;
            }
        }

        public Vector3[][] GetOutput()
        {
            var nativeOutput = m_output.AsArray().Reinterpret<Vector3>();
            var outputCount = m_outputAccessIndices.Length;
            var output = new Vector3[outputCount][];

            var prevIndex = 0;
            for (int i = 0; i < outputCount; ++i)
            {
                var index = m_outputAccessIndices[i];
                var length = index - prevIndex;
                output[i] = new Vector3[length];
                if (length == 0)
                    continue;
                NativeArray<Vector3>.Copy(nativeOutput, prevIndex, output[i], 0, length);
                prevIndex = index;
            }
            
            m_boundsToCheck.Dispose();
            m_output.Dispose();
            m_outputAccessIndices.Dispose();
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
            m_replaceMode = _cell.IsType(Cell.Type.Slime) || _cell.IsType(Cell.Type.Fire) || _cell.IsType(Cell.Type.Bounce);
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
    
    public void UpdateFluids()
    {
        var job = new UpdateFluidsJob(m_cells, m_cellPropertiesNative, m_fluidCells, m_levelWidth, m_levelHeight);
        //job.Execute();
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
            var rand = m_random.NextFloat(0f, 1f);
            if (_cell.IsType(Cell.Type.Smoke) && rand > 0.995f)
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