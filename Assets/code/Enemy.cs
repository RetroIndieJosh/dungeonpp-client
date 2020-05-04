using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;

public class Enemy : MonoBehaviour {
    private const int POS_MULTIPLIER = 1000;

    [SerializeField]
    private int m_startPowerLevel = 1;

    [SerializeField]
    private SpriteRenderer m_renderer = null;

    [SerializeField]
    private AiShooter m_shooter = null;

    [SerializeField]
    private TextMeshPro m_levelTextMesh = null;

    [SerializeField]
    private TextMeshPro m_ownerTextMesh = null;

    private Rigidbody m_body = null;
    private Health m_health = null;

    private int m_ownerId = -1;
    private int m_powerLevel = 1;

    private bool m_initialized = false;
    private bool m_isBeingCreated = false;

    private string m_ownerName = "none";

    public bool IsBeingCreated {  set { m_isBeingCreated = value; } }
    public int OwnerId { set { m_ownerId = value; } }
    public string OwnerName { set { m_ownerName = value; } }
    public int PowerLevel {  get { return m_powerLevel; } }

    public void AddCrystals( int a_amount ) {
        var inventory = GetComponent<Inventory>();
        var crystalList = JimJamManager.instance.GetCrystals( a_amount );
        foreach( var crystal in crystalList )
            inventory.Add( crystal.gameObject );
    }

    public void IncrementPowerLevel() {
        ++m_powerLevel;
        UpdateSprite();
        UpdateText();
    }

    public void Load( EnemyData a_data ) {
        //Debug.LogFormat( "[Enemy] Load enemy: {0}", a_data );

        AddCrystals( a_data.crystals );
        m_startPowerLevel = a_data.powerLevel;
        m_ownerName = a_data.ownerName;
        m_ownerId = a_data.ownerId;
        transform.position = new Vector3( a_data.x / POS_MULTIPLIER, a_data.y / POS_MULTIPLIER, 0.0f );

        UpdateSprite();
        UpdateText();
    }

    public void ResetPowerLevel() {
        m_powerLevel = 0;
    }

    private int CrystalValueTotal {
        get {
            var crystals = 0;
            var inventory = GetComponent<Inventory>();
            for ( int i = 0; i < inventory.Count; ++i ) {
                crystals += inventory[i].GetComponent<Crystal>().Value;
            }
            return crystals;
        }
    }

    public EnemyData ToData() {
        return new EnemyData {
            crystals = CrystalValueTotal,
            x = Mathf.FloorToInt( transform.position.x * POS_MULTIPLIER ),
            y = Mathf.FloorToInt( transform.position.y * POS_MULTIPLIER ),
            powerLevel = m_powerLevel,
            ownerName = m_ownerName,
            ownerId = m_ownerId
        };
    }

    private void Awake() {
        m_body = GetComponent<Rigidbody>();
        m_health = GetComponent<Health>();
    }

    private void Start() {
        var inventory = GetComponent<Inventory>();
        inventory.DroppedItemStayFrames = JimJamManager.instance.CrystalStayTimeFrames;
        inventory.AfterDrop.AddListener( TileMap.instance.AddCrystals );

        ResetPowerLevel();
        for( int i = 0; i < m_startPowerLevel; ++i )
            IncrementPowerLevel();
    }

    private void Update() {
        if ( !m_initialized && !m_isBeingCreated )
            Init();

        if ( m_health != null && m_body != null && m_health.IsDead )
            m_body.velocity = Vector3.zero;
    }

    private void Init() {
        m_powerLevel = Mathf.Max( m_powerLevel, 1 );
        UpdateText();

        var health = GetComponent<Health>();
        if ( health != null ) {
            health.SetRange( 0, m_powerLevel );
            health.ResetToMaximum();
        }

        //m_shooter.SetStats( 180 - m_powerLevel * 10, 5.0f + 0.5f * m_powerLevel );
        var damager = GetComponent<Damager>();
        if ( damager != null )
            damager.Damage = m_powerLevel * 10.0f;

        var inventory = GetComponent<Inventory>();
        if ( inventory != null && inventory.Count == 0 )
            AddCrystals( 1 );

        m_initialized = true;
    }

    private void UpdateSprite() {
        m_renderer.sprite = JimJamManager.instance.GetEnemySprite( m_powerLevel );
    }

    private void UpdateText() {
        if( m_levelTextMesh != null ) m_levelTextMesh.text = "L" + m_powerLevel;
        if ( m_ownerTextMesh != null ) m_ownerTextMesh.text = m_ownerName;
    }
}
