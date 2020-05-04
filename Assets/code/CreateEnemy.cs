using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class CreateEnemy : MonoBehaviour {
    const int INITIAL_TNPL = 1;

    [SerializeField]
    private Enemy m_peacefulEnemyPrefab = null;

    [SerializeField]
    private Counter m_crystalCounter = null;

    [SerializeField]
    private int m_drainPerSecondInit = 1;

    [SerializeField]
    private float m_drainAcceleratePerSec = 1.2f;

    private Enemy m_enemy = null;
    private float m_crystalsSpent = 0.0f;
    private int m_crystalsSpentTotal = 0;
    private int m_toNextPowerLevel = INITIAL_TNPL;
    private float m_drainPerSecond;
    private float m_drainTimeElapsed = 0.0f;

    private Crystal m_crystal = null;

    private List<Enemy> m_enemyList = new List<Enemy>();

    public void FinalizeRoom() {
        UploadAll();
        TileMap.instance.UnlockMap();
    }

    public void FinishCreate() {
        if ( m_enemy == null ) return;

        m_enemy.IsBeingCreated = false;
        m_crystalCounter.Add( m_crystalsSpent );

        m_enemy.AddCrystals( Mathf.CeilToInt( m_crystalsSpentTotal * 0.5f ) );

        m_enemyList.Add( m_enemy );
        m_enemy = null;

        TileMap.instance.IncrementEnemyCount();
    }

    public void ResetRoom() {
        foreach ( var enemy in m_enemyList )
            Destroy( enemy.gameObject );
        m_enemyList.Clear();
        TileMap.instance.ResetEnemyCount();
    }

    public void StartCreate() {
        if ( JimJamManager.instance.AlwaysCreateMode == false 
            && PlayerCharacter.instance.State != PlayerState.Create )
            return;

        if( TileMap.instance.EnemyCount >= JimJamManager.instance.EnemeyMaxPerMap )
            return;

        SetMoveCrystalValue( 1 );

        if ( m_crystalCounter.Count < INITIAL_TNPL ) return;

        m_crystalsSpent = 0.0f;
        m_crystalsSpentTotal = 0;
        m_toNextPowerLevel = INITIAL_TNPL;

        m_enemy = Instantiate( m_peacefulEnemyPrefab, transform.position + transform.up, Quaternion.identity );
        m_enemy.IsBeingCreated = true;
        m_enemy.OwnerId = PlayerCharacter.instance.Id;
        m_enemy.OwnerName = PlayerCharacter.instance.FullName;

        m_drainPerSecond = m_drainPerSecondInit;
        m_drainTimeElapsed = 0.0f;

        StartCoroutine( MoveCrystal() );

        m_enemy.ResetPowerLevel();
        IncrementPowerLevel();
    }

    public void UpdateCreate() {
        if ( m_enemy == null ) return;

        m_drainTimeElapsed += Time.deltaTime;
        if( m_drainTimeElapsed > 1.0f ) {
            m_drainPerSecond *= m_drainAcceleratePerSec;
            m_drainTimeElapsed = 0.0f;
        }

        var spent = m_drainPerSecond * Time.deltaTime;
        if ( m_crystalCounter.Count < spent ) {
            FinishCreate();
            return;
        }

        m_crystalCounter.Add( -spent );
        m_crystalsSpent += spent;

        if ( m_crystalsSpent > m_toNextPowerLevel )
            IncrementPowerLevel();
    }

    private void IncrementPowerLevel() {
        m_enemy.IncrementPowerLevel();
        m_crystalsSpentTotal += Mathf.FloorToInt( m_toNextPowerLevel );
        m_crystalsSpent -= m_toNextPowerLevel;
        m_toNextPowerLevel += m_enemy.PowerLevel;
    }

    private IEnumerator MoveCrystal() {
        m_crystal.GetComponent<SpriteRenderer>().enabled = true;

        while ( m_enemy != null ) {
            var timeElapsed = 0.0f;
            var moveTime = m_crystal.Value/ m_drainPerSecond;
            Debug.LogFormat( "Move time {0}/{1}", moveTime, 1.0f / 30.0f );
            while ( timeElapsed < moveTime && m_enemy != null ) {
                var t = timeElapsed / moveTime;
                m_crystal.transform.position = Vector3.Lerp( transform.position, m_enemy.transform.position, t );
                timeElapsed += Time.deltaTime;
                yield return null;
            }
            if ( moveTime < 1.0f / 6.0f ) SetMoveCrystalValue( m_crystal.Value * 2, true );
        }

        m_crystal.GetComponent<SpriteRenderer>().enabled = false;
    }

    private void SetMoveCrystalValue( int a_value, bool a_visible = false  ) {
        var crystalList = JimJamManager.instance.GetCrystals( a_value );
        //Debug.LogFormat( "For value {0} got {1} crystals", a_value, crystalList.Count );
        var crystalPrefab = crystalList[0];
        if ( m_crystal != null ) Destroy( m_crystal.gameObject );

        m_crystal = Instantiate( crystalPrefab, Vector3.forward * 1000.0f, Quaternion.identity );
        m_crystal.name = "Create Enemy Crystal";

        Destroy( m_crystal.GetComponent<BlinkSprite>() );
        Destroy( m_crystal.GetComponent<Rigidbody>() );

        m_crystal.GetComponent<Collider>().enabled = false;

        var spriteRenderer = m_crystal.GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = crystalPrefab.GetComponent<SpriteRenderer>().sprite;
        spriteRenderer.enabled = a_visible;

        m_crystal.transform.SetParent( transform );
    }

    private void UploadAll() {
        var enemyDataList = new List<EnemyData>();
        foreach ( var enemy in m_enemyList ) {
            var enemyData = enemy.ToData();
            enemyDataList.Add( enemyData );
        }
        ResetRoom();

        foreach( var enemyData in enemyDataList ) {
            StartCoroutine( UploadEnemyData( enemyData ) );
        }

        int loops = 0;
        while( true ) {
            ++loops;
            if ( loops > 10000 ) break;

            var allEnemiesUploaded = true;
            foreach ( var enemyData in enemyDataList ) {
                if ( enemyData.isUploaded == false ) {
                    allEnemiesUploaded = false;
                    break;
                }
            }
            if ( allEnemiesUploaded ) break;
        }
    }

    private IEnumerator UploadEnemyData( EnemyData a_data ) {
        if ( JimJamManager.instance.IsLoaded == false ) yield break;

        a_data.mapId = TileMap.instance.CurrentId;

        Debug.LogFormat( "[Create Enemy] Uploading enemy data to server: {0}", a_data );
        var mapId = TileMap.instance.CurrentId;
        var url = new DungeonUrl( "create_enemy.php" );
        url.AddGet( "map_id", a_data.mapId );
        url.AddGet( "power_level", a_data.powerLevel );
        url.AddGet( "crystals", a_data.crystals );
        url.AddGet( "x", a_data.x );
        url.AddGet( "y", a_data.y );
        url.AddGet( "owner_id", a_data.ownerId );
        var www = UnityWebRequest.Get( url.ToString() );
        yield return www.SendWebRequest();

        if ( www.isNetworkError || www.isHttpError ) {
            Debug.LogError( www.error );
            yield break;
        }

        var result = www.downloadHandler.text.Trim();
        if ( result.StartsWith( "ERR" ) ) {
            url.Error( result );
            yield break;
        }

        Debug.Log( "Upload success!" );
    }
}
