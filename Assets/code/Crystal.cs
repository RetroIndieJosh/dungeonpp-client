using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Crystal : MonoBehaviour {
    public int Value {
        get { return m_collectible.collectValue; }
        private set { m_collectible.collectValue = value; }
    }

    private Collectible m_collectible = null;
    private bool m_isCombining = false;

    private void Awake() {
        m_collectible = GetComponent<Collectible>();
    }

    private void OnCollisionEnter( Collision collision ) {
        if ( m_isCombining ) return;

        var crystal = collision.gameObject.GetComponent<Crystal>();
        if ( crystal == null || crystal.Value != Value ) return;

        m_isCombining = true;

        var crystalList = JimJamManager.instance.GetCrystals( Value * 2 );
        if ( crystalList.Count != 1 ) return;

        crystal.m_isCombining = true;

        //gameObject.SetActive( false );
        //collision.gameObject.SetActive( false );

        Destroy( gameObject );
        Destroy( collision.gameObject );

        //Instantiate( crystalList[0].gameObject, transform.position + Vector3.down * 2.0f, Quaternion.identity );

        var diff = collision.transform.position - transform.position;
        var pos = transform.position + diff * 0.5f;
        var newCrystal = Instantiate( crystalList[0].gameObject, pos, Quaternion.identity );
        Destroy( newCrystal, JimJamManager.instance.CrystalStayTimeSec );
    }

    private void Start() {
        m_blinkSprite = GetComponent<BlinkSprite>();
    }

    private BlinkSprite m_blinkSprite = null;
    private float m_timeElapsedSec = 0.0f;
    private void Update() {
        m_timeElapsedSec += Time.deltaTime;
        if( m_blinkSprite != null ) 
            m_blinkSprite.IsBlinking = m_timeElapsedSec > 0.75f * JimJamManager.instance.CrystalStayTimeSec;
    }
}
