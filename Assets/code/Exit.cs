using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

public class Exit : MonoBehaviour {
    [SerializeField]
    Direction m_direction;

    [SerializeField]
    private SpriteRenderer m_spriteRenderer = null;

    [SerializeField]
    private Collider m_collider = null;

    [SerializeField]
    private UnityEvent m_onLocked = null;

    [SerializeField]
    private UnityEvent m_onUnlocked = null;

    [SerializeField]
    private UnityEvent m_onClose = null;

    [SerializeField]
    private UnityEvent m_onOpen = null;

    [SerializeField, Tooltip("If true, repeatedly check for locked state as fast as possible (heavy web traffic)")]
    private bool m_recurseCheck = false;

    private bool m_isLocked = false;

    public IEnumerator CheckLockedCoroutine( int a_curRoomX, int a_curRoomY ) {
        var dirVec = m_direction.ToVector2();
        var x = a_curRoomX + Mathf.FloorToInt( dirVec.x );
        var y = a_curRoomY + Mathf.FloorToInt( dirVec.y );

        ///Debug.LogFormat( "[Exit] Checking lock to ({0}, {1})", x, y );

        {
            var url = new DungeonUrl( "is_map_locked.php" );
            url.AddGet( "x", x );
            url.AddGet( "y", y );
            var www = UnityWebRequest.Get( url );
            yield return www.SendWebRequest();

            if ( www.isNetworkError || www.isHttpError ) {
                Debug.LogError( www.error );
                yield break;
            }

            var resultText = www.downloadHandler.text.Trim();

            if ( string.IsNullOrEmpty( resultText ) || resultText.StartsWith( "ERR" ) ) {
                url.Error( resultText );
                yield break;
            }

            var prevLocked = m_isLocked;
            m_isLocked = int.Parse( resultText ) == 1;

            if ( prevLocked != m_isLocked ) {
                if ( m_isLocked ) m_onLocked.Invoke();
                else m_onUnlocked.Invoke();
            }
        }

        if ( PlayerCharacter.instance.CharacterClass == CharacterClass.Destroyer ) {
            var url = new DungeonUrl( "get_map.php" );
            url.AddGet( "x", x );
            url.AddGet( "y", y );
            var www = UnityWebRequest.Get( url );
            yield return www.SendWebRequest();

            if ( www.isNetworkError || www.isHttpError ) {
                Debug.LogError( www.error );
                yield break;
            }

            var resultText = www.downloadHandler.text.Trim();

            if ( string.IsNullOrEmpty( resultText ) || resultText.StartsWith( "ERR" ) ) {
                url.Error( resultText );
                yield break;
            }

            var prevLocked = m_isLocked;
            m_isLocked = resultText == "0";

            if ( prevLocked != m_isLocked ) {
                if ( m_isLocked ) m_onLocked.Invoke();
                else m_onUnlocked.Invoke();
            }
        }

        if ( m_recurseCheck && gameObject.activeSelf )
            StartCoroutine( CheckLockedCoroutine( a_curRoomX, a_curRoomY ) );
    }

    public void Close() {
        m_onClose.Invoke();
    }

    public void TryOpen() {
        if ( m_isLocked ) return;
        m_onOpen.Invoke();
    }

    private void Awake() {
        m_spriteRenderer = GetComponent<SpriteRenderer>();
        m_collider = GetComponent<Collider>();
    }
}
