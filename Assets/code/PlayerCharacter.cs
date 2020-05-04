using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;

public enum PlayerState
{
    GeneratingFirstName,
    GeneratingFamilyName,
    DeterminingClass,
    InGame,
    Create
}

[System.Serializable]
public class PlayerData
{
    public int id = -1;
    public string name = "No One";
}

public enum CharacterClass
{
    None,
    Creator,
    Destroyer
}

public class PlayerCharacter : MonoBehaviour {
    static public PlayerCharacter instance = null;

    [SerializeField]
    private bool m_autoGenerate = false;

    [SerializeField, Tooltip( "Class for the player if auto generated" )]
    private CharacterClass m_autoClass = CharacterClass.Creator;

    [SerializeField]
    private TextMeshProUGUI m_statusText = null;

    [SerializeField]
    private TextMeshProUGUI m_nameText = null;

    public CharacterClass CharacterClass {  get { return m_class; } }

    public int Id {  get { return m_id; } }
    public string FullName {  get { return m_firstName + " " + m_familyName; } }

    public PlayerState State {
        get { return m_state; }
        set {
            m_state = value;
            switch ( m_state ) {
                case PlayerState.GeneratingFamilyName:
                    transform.position = m_startPos;
                    DetermineFamilyName();
                    break;
                case PlayerState.GeneratingFirstName:
                    transform.position = m_startPos;
                    if ( m_autoGenerate ) {
                        DetermineFirstName();
                        m_class = m_autoClass;
                        JimJamManager.instance.StartGameAfter( 1.0f );
                        break;
                    }

                    // start second round
                    if( m_isNewPlayer == false ) 
                        JimJamManager.instance.ShowYesNoPortals();
                    DetermineFirstName();
                    break;
                case PlayerState.DeterminingClass:
                    m_nameText.text = FullName + " the " + m_class;
                    if ( m_isNewPlayer ) {
                        m_class = CharacterClass.Creator;
                        State = PlayerState.InGame;
                        m_isNewPlayer = false;
                        break;
                    }
                    DetermineClass();
                    break;
                case PlayerState.InGame:
                    m_nameText.text = FullName + " the " + m_class;
                    JimJamManager.instance.Load();
                    break;
                case PlayerState.Create:
                    JimJamManager.instance.FadeOutIn( null );
                    m_statusText.text = "Hold C to create enemies\nGreen to finalize & upload\nRed to reset the room";
                    break;
            }
        }
    }

    private PlayerState m_state = PlayerState.GeneratingFirstName;

    private CharacterClass m_class = CharacterClass.None;

    private int m_id = -1;
    private string m_firstName;
    private string m_familyName;
    private string m_familyPassword;
    private bool m_isNewPlayer = false;

    [SerializeField]
    CreateEnemy m_enemyCreator = null;

    Vector3 m_startPos = Vector3.up * 2.0f;

    public void AnswerYes() {
        JimJamManager.instance.FadeOutIn( delegate () {
            transform.position = m_startPos;
            switch ( m_state ) {
                case PlayerState.GeneratingFamilyName: State = PlayerState.GeneratingFirstName; break;
                case PlayerState.GeneratingFirstName:
                    State = PlayerState.DeterminingClass;
                    break;
                case PlayerState.DeterminingClass:
                    m_class = CharacterClass.Creator;
                    GetComponentInChildren<SpriteRenderer>().color = Color.green;
                    State = PlayerState.InGame;
                    break;
                case PlayerState.Create:
                    m_enemyCreator.FinalizeRoom();
                    NextGeneration();
                    break;
            }
        } );
    }

    public void AnswerNo() {
        JimJamManager.instance.FadeOutIn( delegate () {
            transform.position = m_startPos;
            switch ( m_state ) {
                case PlayerState.GeneratingFirstName:
                case PlayerState.GeneratingFamilyName: State = m_state; break;
                case PlayerState.DeterminingClass:
                    m_class = CharacterClass.Destroyer;
                    State = PlayerState.InGame;
                    GetComponentInChildren<SpriteRenderer>().color = Color.red;
                    break;
                case PlayerState.Create:
                    m_enemyCreator.ResetRoom();
                    break;
            }
        } );
    }

    public void NextGeneration() {
        JimJamManager.instance.ResetGame();
        State = PlayerState.GeneratingFirstName;
    }

    private void Awake() {
        if( instance != null ) {
            Debug.LogErrorFormat( "[Player Character] Duplicate in {0}; destroying", name );
            Destroy( this );
            return;
        }
        instance = this;
    }

    private void Start() {
        if ( JimJamManager.instance.DoNotLoad ) return;

        // TODO login to preexisting player (family name) account

        StartCoroutine( LoadPlayer() );

        if( m_autoGenerate ) {
            DetermineFirstName();
            DetermineFamilyName();
            m_class = m_autoClass;
            State = PlayerState.InGame;
            return;
        }

        // new player
        m_isNewPlayer = true;
        JimJamManager.instance.ShowYesNoPortals();
        State = PlayerState.GeneratingFamilyName;
    }

    private void Update() {
    }

    private void DetermineClass() {
        m_statusText.text = string.Format( "Red: Destroyer --- Green: Creator" );
    }

    private void DetermineFirstName() {
        m_firstName = GenerateRandomName();
        m_statusText.text = string.Format( "Full name: {0}, okay?", FullName );
    }

    private void DetermineFamilyName() {
        do {
            m_familyName = GenerateRandomName();
        } while ( IsUsed( m_familyName ) );

        m_statusText.text = string.Format( "Family name: {0}, okay?", m_familyName );
    }

    private bool IsUsed( string a_name ) {
        // check if name is used for either first or family name
        return false;
    }

    private string GenerateRandomName( bool a_unique = false ) {
        const string consonant = "bcdfghjklmnpqrstvwxz";
        const string vowel = "aeiou";

        var length = Random.Range( 4, 8 );
        var name = "";
        for ( int i = 0; i < length; ++i ) {
            if( i % 2 == 0 ) {
                var ci = Random.Range( 0, consonant.Length );
                name += consonant[ci];
            } else {
                var vi = Random.Range( 0, vowel.Length );
                name += vowel[vi];
            }
        }

        return name.Substring( 0, 1 ).ToUpper() + name.Substring( 1 );
    }

    private string GeneratePassword() {
        return "password";
    }

    public IEnumerator LoadPlayer() {
        var url = new DungeonUrl( "create_player.php" );
        var www = UnityWebRequest.Get( url.ToString() );
        yield return www.SendWebRequest();

        if ( www.isNetworkError || www.isHttpError ) {
            Debug.LogError( www.error );
            yield break;
        }

        var result = www.downloadHandler.text;
        if ( result.StartsWith( "ERR" ) ) {
            url.Error( result );
            yield break;
        }

        var playerData = JsonUtility.FromJson<PlayerData>( result );
        m_id = playerData.id;
        m_nameText.text = "Player " + m_id;
    }
}
