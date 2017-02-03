using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum TileType : byte
{
    EMPTY,
    ROAD,
    BUILDING
}

public enum VisualizationMode : byte
{
    BLOCKED,
    TYPES
}

public class Grid : MonoBehaviour
{
    /// <summary>
    /// Singleton pattern instance.
    /// </summary>
    public static Grid instance;

    /// <summary>
    /// The size of the grid (in tiles) on the x-axis.
    /// </summary>
    public int gridSizeX;

    /// <summary>
    /// The size of the grid (in tiles) on the x-axis.
    /// </summary>
    public int gridSizeY;

    /// <summary>
    /// The type of every tile, indexed by <see cref="TilePosToIndex(int, int)"/>
    /// </summary>
    private TileType[] tileTypes;

    /// <summary>
    /// Defines the blocking state of every tile, indexed by <see cref="TilePosToIndex(int, int)"/>
    /// </summary>
    private bool[] tileBlocks;

	/// <summary>
    /// The selected start-point for A* Pathfinding
    /// </summary>
	private GameObject startNode;

	/// <summary>
    /// The selected end-point for A* Pathfinding
    /// </summary>
	private GameObject endNode;

	/// <summary>
    /// Dictionary of cumulative costs for each index from start-point
    /// </summary>
	private Dictionary<string,int> GCost;

    /// <summary>
    /// The visualization of each tile, indexed by <see cref="TilePosToIndex(int, int)"/>
    /// </summary>
    private GameObject[] visualizations;

    /// <summary>
    /// What is actually visualized?
    /// </summary>
    public VisualizationMode mode;
    private VisualizationMode _prevMode;

    [Header("Debug")]
    public int debugX;
    public int debugY;
    public TileType debugType;

    [ContextMenu("Debug Set Type")]
    public void DebugSetType()
    {
        SetTileType(this.debugX, this.debugY, this.debugType);
    }

    [ContextMenu("Debug Set Blocked")]
    public void DebugSetBlock()
    {
        SetTileBlocked(this.debugX, this.debugY, true);
    }

    [ContextMenu("Debug Set Unblocked")]
    public void DebugSetUnblocked()
    {
        SetTileBlocked(this.debugX, this.debugY, false);
    }

	[Header("Random Tile Generation")]
    public int seed;
    public int tileForests; //number of origins to build block "forests"
    public float tileFrequency; //0...1 chance of being designated a blocked tile in density generation
    public int tileDensity; 	//size of blocked "forests"

	[Header("A* Pathfinding")]
	/// <summary>
    /// Parent object to contain all pathfinder visuals
    /// </summary>
	public GameObject pathfinder;

    public int TilePosToIndex(int x, int y)
    {
        return x + y * this.gridSizeY;
    }

    public void IndexToTilePos(int index, out int x, out int y)
    {
        x = index % this.gridSizeX;
        y = Mathf.FloorToInt(index / (float)this.gridSizeX);
    }

    #region Getter and Setter

    public void SetTileType(int index, TileType type)
    {
        this.tileTypes[index] = type;
        UpdateTile(index);
    }

    public void SetTileType(int x, int y, TileType type)
    {
        SetTileType(TilePosToIndex(x, y), type);
    }

    public TileType GetTileType(int index)
    {
        return this.tileTypes[index];
    }

    public TileType GetTileType(int x, int y)
    {
        return GetTileType(TilePosToIndex(x, y));
    }

    public void SetTileBlocked(int index, bool blocked)
    {
        this.tileBlocks[index] = blocked;
        UpdateTile(index);
    }

    public void SetTileBlocked(int x, int y, bool blocked)
    {
        SetTileBlocked(TilePosToIndex(x, y), blocked);
    }

    public bool IsTileBlocked(int index)
    {
        return this.tileBlocks[index];
    }

    public bool IsTileBlocked(int x, int y)
    {
        return IsTileBlocked(TilePosToIndex(x, y));
    }

	public bool IsTileNotBlocked(int index)
    {
        return !this.tileBlocks[index];
    }

    public bool IsTileNotBlocked(int x, int y)
    {
        return IsTileBlocked(TilePosToIndex(x, y));
    }


    #endregion

    public void Awake()
    {
        if (instance != null)
        {
            Debug.LogError("Singleton pattern violated!");
            return;
        }

        _prevMode = this.mode;
        instance = this;
        this.tileTypes = new TileType[this.gridSizeX * this.gridSizeY];
        this.tileBlocks = new bool[this.gridSizeX * this.gridSizeY];
        Random.InitState(seed);

        // Init rendering
        this.visualizations = new GameObject[this.gridSizeX * this.gridSizeY];
        int x = 0, y = 0;
        for (int i = 0; i < this.tileBlocks.Length; i++)
        {
            IndexToTilePos(i, out x, out y);
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.parent = this.transform;
            go.transform.position = new Vector3(x, 0, y);
            this.visualizations[i] = go;

            UpdateTile(i);
        }

		RandomizeBlockedTiles();
    }

    public void Update()
    {
        if (this._prevMode != this.mode)
        {
            // Update all!
            for (int i = 0; i < this.tileBlocks.Length; i++)
            {
                UpdateTile(i);
            }
			_prevMode = this.mode;
        }

        //Pathfinding markers
		if (Input.GetMouseButtonDown (0)) {
			RaycastHit hitInfo = new RaycastHit ();

			if (Physics.Raycast (Camera.main.ScreenPointToRay (Input.mousePosition), out hitInfo)) {
				GameObject obj = hitInfo.collider.gameObject;

				if (!startNode && !IsTileBlocked((int)obj.transform.position.x, (int)obj.transform.position.z)) {
					startNode = obj;
					spawnPole(startNode.transform.position, 10f);
				} else if(!endNode && !IsTileBlocked((int)obj.transform.position.x, (int)obj.transform.position.z)) {
					endNode = obj;
					spawnPole(endNode.transform.position, 10f);
					Search_AStar(TilePosToIndex((int)startNode.transform.position.x, (int)startNode.transform.position.z),TilePosToIndex((int)endNode.transform.position.x, (int)endNode.transform.position.z));
				}
			}
		}
    }

    private void UpdateTile(int index)
    {
        var mr = this.visualizations[index].GetComponent<MeshRenderer>();
        MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

        if (this.mode == VisualizationMode.TYPES)
        {
            switch (this.tileTypes[index])
            {
                case TileType.EMPTY: propertyBlock.SetColor("_Color", Color.white); break;
                case TileType.BUILDING: propertyBlock.SetColor("_Color", Color.blue); break;
                case TileType.ROAD: propertyBlock.SetColor("_Color", Color.black); break;
            }
        }
        else
        {
            propertyBlock.SetColor("_Color", this.tileBlocks[index] ? Color.red : Color.green);
        }


        mr.SetPropertyBlock(propertyBlock);
    }

    //Init Blocked tile generation
    public void RandomizeBlockedTiles()
    {
		CreateForestOrigins();

    	IncreaseForestDensity(tileDensity);

    }

	//Creating origin points for blocked "forests"
    public void CreateForestOrigins ()
	{
		for(int i = 0; i < tileForests; i++)
		{
			int randomIndex = Random.Range(0,tileBlocks.Length);
    		randomBlockTile(randomIndex, 1);
    	}

	}

	//Random change to set tile as blocked
	private void randomBlockTile (int index, float frequency)
	{
		if (Random.value < frequency) {
			SetTileBlocked (index, true);
			UpdateTile (index);
		}
	}

	//Find each origin index, then grow forest density from each origin
	public void IncreaseForestDensity (int density)
	{
		List<int> _originIndex = new List<int> ();

		for (int i = 0; i < tileBlocks.Length; i++) {
			if (IsTileBlocked(i))
				_originIndex.Add (i);
		}

		foreach (int index in _originIndex) {
			growBlockedOrigin(density, index);
		}
	}

	//For each iteration up to density size, this will get all surrounding tiles and try to mark as blocked
	private void growBlockedOrigin (int density, int index)
	{
		HashSet<int> forestTiles = new HashSet<int> ();
		forestTiles.Add (index);
		HashSet<int> _foundTiles = new HashSet<int> ();

		for (int currentDensity = 0; currentDensity < density; currentDensity++) 
		{
			foreach (int _index in forestTiles) 
			{
				_foundTiles.UnionWith(GetSurroundingTiles (_index));

			}

			forestTiles.UnionWith (_foundTiles);
			_foundTiles.Clear ();

			foreach (int _index in forestTiles) 
			{
				randomBlockTile (_index, tileFrequency);

			}
			forestTiles.RemoveWhere(IsTileNotBlocked);
		}
	}

	//Returns surrounding tiles of index
	private HashSet<int> GetSurroundingTiles (int index)
	{
		HashSet<int> surroundingTiles = new HashSet<int>();
		 
		int _x;
		int _y;

		IndexToTilePos(index, out _x, out _y);

		if(_x < gridSizeX-1)
			surroundingTiles.Add(TilePosToIndex(_x+1,_y));
		if(_x > 0)
			surroundingTiles.Add(TilePosToIndex(_x-1,_y));
		if(_y < gridSizeY-1)
			surroundingTiles.Add(TilePosToIndex(_x,_y+1));
		if(_y > 0)
			surroundingTiles.Add(TilePosToIndex(_x,_y-1));

		return surroundingTiles;
	}


	public void ClearPathfindingNodes(){
		startNode = null;
		endNode = null;
		clearPoles();
	}

	void spawnPole(Vector3 pos, float height){
		GameObject gos = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
		gos.transform.parent = pathfinder.transform;
		gos.transform.position = pos;

	}

	void clearPoles ()
	{
		foreach (Transform child in pathfinder.transform) {
			GameObject.Destroy (child.gameObject);
		}
	}

	void Search_AStar (int indexStartTile, int indexEndTile)
	{
		GCost = new Dictionary<string, int> ();
		Dictionary<string, int> indexParent = new Dictionary<string, int> ();
		HashSet<int> reviewIndex = new HashSet<int> ();
		List<int> pathIndex = new List<int> ();
		HashSet<int> adjacentIndex = new HashSet<int> ();

		//Init starting tile
		reviewIndex.Add (indexStartTile);
		GCost.Add (indexStartTile.ToString (), 0);
		do {
			int currentTile = FindLowestCostTile (reviewIndex, indexEndTile);
			pathIndex.Add (currentTile);
			reviewIndex.Remove (currentTile);

			if (pathIndex.Contains (indexEndTile))
				break; //Path is found

			adjacentIndex = GetSurroundingTiles (currentTile);
			adjacentIndex.RemoveWhere (IsTileBlocked);

			foreach (int _index in adjacentIndex) {
				if (pathIndex.Contains (_index))
					continue;

				if (!reviewIndex.Contains (_index)) {
					reviewIndex.Add (_index);
					indexParent.Add (_index.ToString (), currentTile);
					GCost.Add (_index.ToString(), CalculateGCost (currentTile, 1));
				} else {
					if (GCost [_index.ToString ()] > CalculateGCost (currentTile, 1)) {
						GCost [_index.ToString ()] = CalculateGCost (currentTile, 1);
						indexParent [_index.ToString ()] = currentTile;
					}

				}
			}
		} while(reviewIndex.Count != 0);

		if (reviewIndex.Count == 0) {
			Debug.Log ("No Path Found");
		} else {
			//Create Path
			List<int> shortestPath = new List<int>();
			int _currentIndex;

			shortestPath.Add(indexEndTile);
			do
			{
				_currentIndex = shortestPath[shortestPath.Count-1];
				if(_currentIndex != indexStartTile)
					shortestPath.Add(indexParent[_currentIndex.ToString()]);

			}while(_currentIndex != indexStartTile);

			//Draw Path
			DrawLine(shortestPath);
		}
	}

	private int FindLowestCostTile (HashSet<int> _reviewSet, int endTile)
	{
		int index = int.MaxValue;
		int cost = int.MaxValue;
		int FCost;

		foreach (int _index in _reviewSet) {
			FCost = CalculateFCost(_index, endTile); 
			if (FCost < cost) {
				cost = FCost;
				index = _index;
			}
		}

		return index;
	}

	private int ManhattanHeuristic(int indexSource, int indexEnd){
		int x1, x2, y1, y2, result;

		IndexToTilePos(indexSource,out x1,out y1);
		IndexToTilePos(indexEnd,out x2,out y2);

		result = Mathf.Abs(x2-x1)+Mathf.Abs(y2-y1);

		return result;
	}

	private int CalculateGCost(int _parentIndex, int _indexCost){
		return GCost[_parentIndex.ToString()]+_indexCost;
	}

	private int CalculateFCost(int _currentIndex, int _endIndex){
		return GCost[_currentIndex.ToString()] + ManhattanHeuristic(_currentIndex, _endIndex);

	}

	private void DrawLine (List<int> _indexList)
	{

		Vector3[] myPath = new Vector3[_indexList.Count];
		int x1,y1;
		for(int i = 0; i < _indexList.Count; i++){
			IndexToTilePos(_indexList[i], out x1, out y1);
			myPath[i] = new Vector3((float)x1, 1.5f, (float)y1);
		}

		GameObject go = new GameObject();
		go.transform.parent = pathfinder.transform;

		go.AddComponent<LineRenderer>();
		LineRenderer lr = go.GetComponent<LineRenderer>();
		lr.startWidth = 0.5f;
		lr.endWidth = 0.5f;
		lr.numPositions=myPath.Length;
		lr.SetPositions(myPath);
	}
}
