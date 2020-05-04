using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Stair : MonoBehaviour {
    private void OnCollisionEnter( Collision collision ) {
        if ( collision.gameObject.tag != "Player" ) return;

        Debug.Log( "Collide with stairs" );

        collision.gameObject.transform.position = Vector3.zero;
        gameObject.SetActive( false );
        //JimJamManager.instance.NextFloor();
    }
}
