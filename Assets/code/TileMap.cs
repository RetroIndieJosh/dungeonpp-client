using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Events;

[System.Serializable]
class TileMapRow
{
    public int[] tile;
}

[System.Serializable]
class TileMapData
{
    public int id;
    public int x;
    public int y;
    public int Height { get { return row == null ? 0 : row.Length; } }
    public int Width { get { return row == null || row.Length == 0 || row[0] == null ? 0 : row[0].tile.Length; } }
    public TileMapRow[] row;
    public EnemyData[] enemyList;
    public bool cleared = false;
}

[System.Serializable]
public class EnemyData {
    public int mapId;
    public int powerLevel;
    public int crystals;
    public int x;
    public int y;
    public int ownerId;
    public string ownerName;
    public bool isUploaded = false;

    public override string ToString() {
        return string.Format( "M{5} L{0} C{1} ({2}, {3}) {4} (#{6})", powerLevel, crystals, x, y, ownerName, mapId, 
            ownerId );
    }
}

// TODO rename to TileMapManager
public class TileMap : MonoBehaviour {
    public static TileMap instance = null;

    static bool s_isUnlocking = false;

    static public bool CanQuit() {
        if ( instance.m_isLocked == false )
            return true;

        if ( s_isUnlocking == false ) {
            instance.UnlockMap();
            s_isUnlocking = true;
        }

        return !instance.m_isLocked;
    }

    [RuntimeInitializeOnLoadMethod]
    static public void RunOnStart() {
        Application.wantsToQuit += CanQuit; 
    }

    [SerializeField]
    private Color m_gizmoColor = Color.red;

    [SerializeField]
    private GameObject m_map = null;

    [SerializeField]
    private float m_mapScrollTime = 8.0f;

    [SerializeField]
    private Exit[] m_exitList = null;

    private Dictionary<Vector2Int, TileMapData> m_mapDataDict = new Dictionary<Vector2Int, TileMapData>();
    private List<int> m_mapIdList = null;
    private bool m_isLoading = false;
    private bool m_isLocked = false;

    private bool m_isNewMap = false;
    private int m_mapId = 0;
    private int m_enemyCount = 0;
    public int CurrentId {  get { return m_mapId; } }
    public int EnemyCount {  get { return m_enemyCount; } }
    public bool IsLoaded {
        get { return m_mapIdList != null && m_mapIdList.Count > 0 && m_mapDataDict.Count == m_mapIdList.Count; }
    }

    public Vector2Int MapCoordinate { get { return m_mapCoordinate; } }
    public string MapCoordinateStr { get { return MapCoordinate.ToString(); } }

    private SpriteRenderer[,] m_tile = new SpriteRenderer[1,1];

    public void IncrementEnemyCount() {
        ++m_enemyCount;
    }

    public void ResetEnemyCount() {
        m_enemyCount = 0;
    }

    public void AddCrystals(List<GameObject> a_crystalList ) {
        foreach ( var crystal in a_crystalList ) crystal.transform.parent = m_map.transform;
    }

    public void CheckExits() {
        if( m_isNewMap ) 
            foreach ( var exit in m_exitList ) exit.Close();

        if ( PlayerCharacter.instance.State != PlayerState.InGame ) return;
        if ( IsLoaded == false || EnemyCount > 0 ) return;

        foreach ( var exit in m_exitList ) exit.TryOpen();
    }

    public void ClearEnemies() {
        foreach( var enemy in FindObjectsOfType<Enemy>() )
            Destroy( enemy.gameObject );
    }

    public void CreateMap() {
        PlayerCharacter.instance.State = PlayerState.Create;
        StartCoroutine( CreateMapCoroutine() );
    }

    public void UnlockMap() {
        StartCoroutine( UnlockMapCoroutine() );
    }

    private IEnumerator UnlockMapCoroutine() {
        Debug.LogFormat( "[Tile Map] Unlock map {0}", m_mapId );

        var url = new DungeonUrl( "unlock_map.php" );
        url.AddGet( "id", m_mapId );
        var www = UnityWebRequest.Get( url.ToString() );
        yield return www.SendWebRequest();

        if ( www.isNetworkError || www.isHttpError ) {
            Debug.LogError( www.error );
            yield break;
        }

        var resultText = www.downloadHandler.text.Trim();
        if ( resultText.StartsWith( "ERR" ) ) {
            url.Error( resultText );
            yield break;
        }

        m_isLocked = false;
        if ( s_isUnlocking ) Application.Quit();
    }

    public void LoadAll() {
        if( m_mapIdList != null ) m_mapIdList.Clear();
        if( m_mapDataDict != null ) m_mapDataDict.Clear();

        m_isLoading = true;
        StartCoroutine( LoadAllCoroutine() );
    }

    private IEnumerator LoadAllCoroutine() {
        m_mapIdList = null;
        yield return StartCoroutine( GetMapIdList() );

        if( m_mapIdList == null ) {
            Debug.LogError( "Failed to get map IDs; aborting load" );
            yield break;
        }

        foreach ( var id in m_mapIdList ) StartCoroutine( LoadCoroutine( id ) );
    }

    private IEnumerator GetMapIdList() {
        var url = new DungeonUrl( "get_map_id_list.php" );
        var www = UnityWebRequest.Get( url.ToString() );
        yield return www.SendWebRequest();

        if ( www.isNetworkError || www.isHttpError ) {
            Debug.LogError( www.error );
            yield break;
        }

        var resultText = www.downloadHandler.text.Trim();
        if ( resultText.StartsWith( "ERR" ) ) {
            url.Error( resultText );
            yield break;
        }

        var splitResult = resultText.Split( ',' );
        if( splitResult.Length == 0 ) {
            Debug.LogError( "[Tile Map] No map IDs returned" );
            yield break;
        }

        m_mapIdList = new List<int>();
        foreach( var valStr in splitResult ) {
            var val = 0;
            if( int.TryParse( valStr, out val ) == false ) {
                Debug.LogFormat( "[Tile Map] Failed to parse '{0}' as int; aborting load", valStr );
                yield break;
            }
            m_mapIdList.Add( val );
        }

        Debug.LogFormat( "[Tile Map] Received {0} map ID values", m_mapIdList.Count );
    }

    Vector2Int m_mapCoordinate = Vector2Int.zero;

    public bool GoToMap() {
        m_isNewMap = false;

        if ( m_mapDataDict.ContainsKey( m_mapCoordinate ) == false ) {
            m_isNewMap = true;
            return false;
        }

        foreach ( var crystal in FindObjectsOfType<Crystal>() ) {
            // HACK avoid prefab crystals (parented to "prefabs")
            if ( crystal.transform.parent == null )
                Destroy( crystal.gameObject );
        }

        // TODO pool tiles
        /*
        m_tile = new SpriteRenderer[m_data.Width, m_data.Width];
        for( int x = 0; x < m_data.Width; ++x ) {
            for ( int y = 0; y < m_data.Height; ++y ) {
                m_tile[x, y] = TileManager.instance.CreateTile( transform, m_data.row[y].tile[x], x, y );
            }
        }
        */
        return true;
    }

    private void LoadEnemies( GameObject a_parent ) {
        var mapData = m_mapDataDict[m_mapCoordinate];

        //Debug.LogFormat( "[Tile Map] Creating enemies for map {0}", m_mapId );
        m_enemyCount = 0;
        if ( mapData != null && mapData.cleared == false && mapData.enemyList != null ) {
            foreach ( var enemyData in mapData.enemyList ) {
                var pos = new Vector2( enemyData.x, enemyData.y );
                var enemy = Instantiate( JimJamManager.instance.EnemyPrefab, a_parent.transform );
                //enemy.GetComponent<Bounds>().enabled = false;
                enemy.transform.localPosition = pos;
                enemy.Load( enemyData );
                ++m_enemyCount;

                var health = enemy.GetComponent<Health>();
                if ( health == null ) continue;
                health.OnDeath.AddListener( delegate { --m_enemyCount; } );
            }
        }

        // setup exits
        foreach ( var exit in m_exitList ) {
            if ( m_isNewMap == false )
                StartCoroutine( exit.CheckLockedCoroutine( m_mapCoordinate.x, m_mapCoordinate.y ) );
            exit.Close();
        }
    }

    private void UpdateDisplayName() {
        var name = MapCoordinateStr;
        if ( PlayerCharacter.instance.State == PlayerState.Create )
            name = "NEW " + name;
        if ( m_isLocked ) name += "**";
        JimJamManager.instance.FloorDisplayName = name;
    }

    public void Move( Direction a_direction ) {
        if( !a_direction.IsCardinal()) {
            Debug.LogErrorFormat( "[Tile Map] Cannot move map {0} in non-cardinal direction {1}", name, a_direction );
            return;
        }

        m_mapDataDict[m_mapCoordinate].cleared = true;

        StartCoroutine( MoveCoroutine(a_direction) );
    }

    public void ReturnToStart() {
        m_mapCoordinate = Vector2Int.zero;
        GoToMap();
    }

    private IEnumerator MoveCoroutine( Direction a_direction ) {
        var dirVec = a_direction.ToVector2();
        var xDiff = Mathf.FloorToInt( dirVec.x );
        var yDiff = Mathf.FloorToInt( dirVec.y );
        m_mapCoordinate.x += xDiff;
        m_mapCoordinate.y += yDiff;

        var height = Camera.main.orthographicSize * 2.0f;
        var width = Camera.main.aspect * height;
        var offset = Vector2.right * width * xDiff + Vector2.up * height * yDiff;

        // load new map in place
        //Debug.Log( "Loading next map" );
        //yield return StartCoroutine( LoadCoroutine( null ) );
        //Debug.Log( "Done loading next map" );

        var newMap = Instantiate( m_map );
        newMap.transform.SetParent( m_map.transform.parent );
        newMap.transform.position = m_map.transform.position + (Vector3)offset;

        var mapExists = GoToMap();

        // scroll map and player together
        var player = GameObject.FindGameObjectWithTag( "Player" );
        var playerBounds = player.GetComponent<Bounds>();
        playerBounds.enabled = false;
        InputManager.instance.IsPaused = true;

        var mapScrollStep = offset / m_mapScrollTime;
        var playerScrollStep = ( offset - new Vector2( xDiff, yDiff ) ) / m_mapScrollTime;
        var timeElapsed = 0.0f;
        while( timeElapsed < m_mapScrollTime ) {
            var dt = Time.unscaledDeltaTime;
            var step = (Vector3)mapScrollStep * dt;
            m_map.transform.position -= step;
            newMap.transform.position -= step;

            var playerStep = (Vector3)playerScrollStep * dt;
            player.transform.position -= playerStep;

            timeElapsed += dt;
            yield return null;
        }

        // unload previous map
        Destroy( m_map );
        m_map = newMap;

        m_map.transform.position = Vector3.zero;

        if ( mapExists ) LoadEnemies( m_map );

        //foreach( var enemy in m_map.GetComponentsInChildren<Enemy>() )
            //enemy.GetComponent<Bounds>().enabled = true;
        playerBounds.enabled = true;
        InputManager.instance.IsPaused = false;

        if ( mapExists == false ) {
            CreateMap();
            JimJamManager.instance.ShowYesNoPortals();
        }
    }

    private void Awake() {
        if( instance != null ) {
            Debug.LogErrorFormat( "[TIle Map] Duplicate in {0}; destroying", name );
            Destroy( this );
            return;
        }
        instance = this;
    }

    private IEnumerator CreateMapCoroutine() {
        var url = new DungeonUrl( "create_map.php" );
        url.AddGet( "x", m_mapCoordinate.x );
        url.AddGet( "y", m_mapCoordinate.y );
        url.AddGet( "width", 10 );
        url.AddGet( "height", 9 );
        url.AddGet( "owner_id", PlayerCharacter.instance.Id );
        var www = UnityWebRequest.Get( url.ToString() );
        yield return www.SendWebRequest();

        if( www.isNetworkError || www.isHttpError) {
            Debug.LogError( www.error );
            yield break;
        }

        var resultText = www.downloadHandler.text.Trim();
        if( resultText.StartsWith("ERR")) {
            url.Error( resultText );
            yield break;
        }

        m_mapId = int.Parse( resultText );
        Debug.LogFormat( "[Tile Map] Created new map {0}", m_mapId );

        m_isLocked = true;

        JimJamManager.instance.ShowNowhere();
    }

    private IEnumerator LoadCoroutine( int a_mapId ) {
        //Debug.LogFormat( "[Tile Map] Retrieving map at ({0}, {1})", m_x, m_y );

        var url = new DungeonUrl( "get_map.php" );
        url.AddGet( "id", a_mapId );
        var www = UnityWebRequest.Get( url.ToString() );
        yield return www.SendWebRequest();

        if( www.isNetworkError || www.isHttpError) {
            Debug.LogError( www.error );
            yield break;
        }

        var resultText = www.downloadHandler.text.Trim();

        if( string.IsNullOrEmpty(resultText ) || resultText.StartsWith("ERR")) {
            url.Error( resultText );
            yield break;
        }

        /*
        m_isNowhere = resultText == "0";

        if( m_isNowhere ) {
            JimJamManager.instance.StartCreateMode();
            yield break;
        }
        */

        var mapData = JsonUtility.FromJson<TileMapData>( resultText );
        m_mapDataDict.Add( new Vector2Int( mapData.x, mapData.y ), mapData );
    }

    private void OnDrawGizmos() {
        /*
        Gizmos.color = m_gizmoColor;
        var width = m_data.Width;
        if ( width == 0 ) width = 1;
        var size = width * Vector3.one;
        Gizmos.DrawWireCube( transform.position + size * 0.5f, size );
        */
    }

    public UnityEvent OnLoaded {  get { return m_onLoaded; } }
    private UnityEvent m_onLoaded = new UnityEvent();

    private void Update() {
        UpdateDisplayName();

        if( m_isLoading ) {
            if ( IsLoaded ) {
                m_isLoading = false;
                m_onLoaded.Invoke();
            }

            return;
        }

        CheckExits();
    }
}
