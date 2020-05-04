using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

[System.Serializable]
class FloorData
{
}

public class DungeonUrl
{
    #region hidden
    #region seriously hidden
    const string BASE_URL = /*redacted*/;
    const string DB_VAL = "0";
    #endregion
    #region seriously hidden
    static public string Token {  get { return /*redacted*/; } }
    #endregion
    #endregion

    public static implicit operator string( DungeonUrl a_url ) {
        return a_url.ToString();
    }

    Dictionary<string, string> m_getVars = new Dictionary<string, string>();
    private string m_page = "index.html";

    public DungeonUrl(string a_page ) {
        m_page = a_page;
        AddGet( "db", DB_VAL );
        AddGet( "token", Token );
    }

    public void AddGet( string a_key, object a_value ) {
        m_getVars[a_key] = a_value.ToString();
    }

    public void Error(string a_msg) {
        Debug.LogErrorFormat( "ERR ({0}: {1}", m_page, a_msg );
    }

    public override string ToString() {
        var url = BASE_URL + m_page + "?";
        foreach ( var pair in m_getVars )
            url += string.Format( "{0}={1}&", pair.Key, pair.Value );
        url = url.Substring( 0, url.Length - 1 );
        return url;
    }
}

// TODO rename to DungeonManager
public class JimJamManager : MonoBehaviour
{
    static public JimJamManager instance = null;

    [SerializeField]
    private int m_enemyMaxPerMap = 8;

    [SerializeField]
    private float m_crystalStayTimeFrames = 30;

    [SerializeField]
    private bool m_loadOnStart = false;

    [SerializeField]
    private GameObject m_nowhereMan = null;

    [SerializeField]
    private Enemy m_enemyPrefab = null;

    [SerializeField]
    private TextMeshProUGUI m_playerIdTextMesh = null;

    [SerializeField]
    private TextMeshProUGUI m_floorTextMesh = null;

    [SerializeField]
    private TextMeshProUGUI m_nowhereTextMesh = null;

    [SerializeField]
    private int m_powerLevelPerSpriteChange = 3;

    [SerializeField]
    private Crystal[] m_crystalPrefabList = null;

    [SerializeField]
    private Sprite[] m_enemySpriteList = null;

    [SerializeField]
    private TextMeshProUGUI m_deathText = null;

    [Header("Debug")]

    [SerializeField]
    public bool DoNotLoad = false;

    [SerializeField]
    public bool AlwaysCreateMode = false;

    private GameObject Player {  get { return PlayerCharacter.instance.gameObject; } }

    public float CrystalStayTimeFrames {  get { return m_crystalStayTimeFrames; } }
    public float CrystalStayTimeSec {  get { return m_crystalStayTimeFrames / 60.0f; } }
    public int EnemeyMaxPerMap {  get { return m_enemyMaxPerMap; } }
    public string FloorDisplayName { set { m_floorTextMesh.text = value; } }

    public Enemy EnemyPrefab { get { return m_enemyPrefab; } }

    public void StartGameAfter( float a_seconds ) {
        StartCoroutine( StartGameAfterCoroutine( a_seconds ) );
    }

    private IEnumerator StartGameAfterCoroutine( float m_seconds ) {
        InputManager.instance.IsPaused = true;
        yield return new WaitForSeconds( m_seconds );
        PlayerCharacter.instance.State = PlayerState.InGame;
        InputManager.instance.IsPaused = false;
    }

    public List<Crystal> GetCrystals( int a_amount ) {
        var crystalList = new List<Crystal>();
        foreach ( var crystalPrefab in m_crystalPrefabList ) {
            var quantity = a_amount / crystalPrefab.Value;
            a_amount -= quantity * crystalPrefab.Value;

            for ( var i = 0; i < quantity; ++i )
                crystalList.Add( crystalPrefab );
        }

        return crystalList;
    }

    public Sprite GetEnemySprite( int a_powerLevel ) {
        var spriteIndex = Mathf.FloorToInt( (a_powerLevel - 1) / (float)m_powerLevelPerSpriteChange );
        spriteIndex = Mathf.Min( spriteIndex, m_enemySpriteList.Length - 1 );
        return m_enemySpriteList[spriteIndex];
    }

    private IEnumerator DeathCoroutine() {
        m_deathText.gameObject.SetActive( true );
        yield return new WaitForSeconds( 2 );
        m_deathText.gameObject.SetActive( false );
        PlayerCharacter.instance.NextGeneration();
    }

    public void ResetGame() { ResetGame( true ); }

    private void ResetGame( bool a_transition ) {
        if ( a_transition ) FadeOutIn( FinishResetGame );
        else FinishResetGame();
    }

    private void FinishResetGame() {
        // reactivate from death
        Player.SetActive( true );

        var health = Player.GetComponent<Health>();
        health.ResetToMaximum();
        health.OnDeath.RemoveAllListeners();
        health.OnDeath.AddListener( delegate () {
            StartCoroutine( DeathCoroutine() );
        } );
        foreach( var enemy in FindObjectsOfType<Enemy>() )
            Destroy( enemy.gameObject );

        TileMap.instance.ReturnToStart();
    }

    [Header("Warp")]

    [SerializeField]
    private RawImage m_fadeImage = null;

    [SerializeField]
    private float m_warpSpinDegPerSec = 1.0f;

    [SerializeField]
    private float m_warpTransitionTime = 1.0f;

    public void FadeOutIn( UnityAction a_afterFadeOut ) {
        StartCoroutine( FadeOutInCoroutine( a_afterFadeOut ) );
    }

    private IEnumerator FadeOutInCoroutine( UnityAction a_whileFadedFunc ) {
        InputManager.instance.IsPaused = true;

        var playerCollider = Player.GetComponent<Collider>();
        playerCollider.enabled = false;

        var timeElapsed = 0.0f;
        while( timeElapsed < m_warpTransitionTime ) {
            var t = timeElapsed / m_warpTransitionTime;
            m_fadeImage.color = Color.Lerp( Color.clear, Color.black, t );
            timeElapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        m_fadeImage.color = Color.black;

        InputManager.instance.IsPaused = false;
        playerCollider.enabled = true;

        if( a_whileFadedFunc != null ) 
            a_whileFadedFunc.Invoke();

        timeElapsed = 0.0f;
        while( timeElapsed < m_warpTransitionTime ) {
            var t = timeElapsed / m_warpTransitionTime;
            m_fadeImage.color = Color.Lerp( Color.clear, Color.black, 1.0f - t );
            timeElapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        m_fadeImage.color = Color.clear;
    }

    private void Awake() {
        if ( instance != null ) {
            Destroy( this );
            return;
        }
        instance = this;
    }

    private void OnApplicationQuit() {
        Debug.Log( "Quit application" );
    }

    private void Start() {
        if ( m_loadOnStart ) Load();
    }

    private void Update() { }

    private bool m_isLoaded = false;
    public bool IsLoaded {  get { return m_isLoaded; } }

    [SerializeField]
    TextMeshProUGUI m_loadingTextMesh = null;

    public void Load() {
        m_fadeImage.color = Color.black;

        TileMap.instance.LoadAll();
        TileMap.instance.OnLoaded.AddListener( delegate {
            ResetGame( false );
            m_isLoaded = true;
            m_loadingTextMesh.gameObject.SetActive( false );
            m_fadeImage.color = Color.clear;
            RemoveNowhere();
        } );
    }
    
    public void ShowYesNoPortals() {
        m_nowhereMan.transform.position = Vector3.zero;
        m_floorTextMesh.text = "???";
        m_nowhereTextMesh.gameObject.SetActive( true );

        m_nowhereMan.GetComponent<SpriteRenderer>().enabled = false;
        m_nowhereMan.GetComponent<Collider>().enabled = false;
    }

    public void ShowNowhere() {
        m_nowhereMan.GetComponent<SpriteRenderer>().enabled = true;
        m_nowhereMan.GetComponent<Collider>().enabled = true;
    }

    public void RemoveNowhere() {
        m_nowhereMan.transform.position = Vector3.one * 1000.0f;
        m_nowhereTextMesh.gameObject.SetActive( false );
    }

    /*
    private IEnumerator LoadMap() {
        var www = UnityWebRequest.Get( BASE_URL + "get_map.php?id=" + m_mapId );
        yield return www.SendWebRequest();

        if( www.isNetworkError || www.isHttpError) {
            Debug.LogError( www.error );
            yield break;
        }

        var result = www.downloadHandler.text;
        if( result.StartsWith("ERR")) {
            Debug.LogError( result );
            yield break;
        }

        m_data = JsonUtility.FromJson<TileMapData>( result );

        // TODO pool tiles
        m_tile = new SpriteRenderer[m_data.Width, m_data.Width];
        for( int x = 0; x < m_data.Width; ++x ) {
            for ( int y = 0; y < m_data.Height; ++y ) {
                m_tile[x, y] = TileManager.instance.CreateTile( transform, m_data.row[y].tile[x], x, y );
            }
        }
    }
    */
}