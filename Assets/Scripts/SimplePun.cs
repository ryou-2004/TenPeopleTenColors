using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
public class SimplePun : MonoBehaviourPunCallbacks
{
    [Header("プレイヤー数(デバッグ用)")] public int peopleNuumber;
    [Header("ゲームシーン名")]public string scene;
    [Header("スタートボタンがある方")] public GameObject title;
    [Header("ロード中&マッチング中")] public GameObject load;
    //[Header("ターン数")]
    //[Header("合計ターン数の設定")] public int endTurn_ins;//全部で何ターンあるか
    [Header("名前")] public Text name_text;

    public BGMController bgmController;

    //public static int endTurn;
    public static bool connect;
    public static bool IsMaster;
    public static int playerNumber;
    public static string playerName;

    private PhotonView view;
    public static int peopleNuumberSta;
    private static bool IsMasterServerConnected;
    private bool isLoad;
    private int isClientLoadCompleted;
    private Coroutine multi;
    private AsyncOperation async;
    private bool isJoin;
    private void Start()
    {
        peopleNuumberSta = peopleNuumber;
        PhotonNetwork.ConnectUsingSettings();//Photonに接続
        view = GetComponent<PhotonView>();
        IsMaster = false;
        playerNumber = 0;
        async = SceneManager.LoadSceneAsync(scene);
        async.allowSceneActivation = false;//ロード後自動シーン遷移をしない
    }
    public void LoadCancel()
    {
        StopCoroutine(multi);
        title.SetActive(true);
        load.SetActive(false);
    }
    //マルチプレイのボタンを押された時の処理
    public void LoadScene()
    {
        StartCoroutine(WaitJoined());
        IEnumerator WaitJoined()
        {
            yield return new WaitWhile(() => PhotonNetwork.NetworkClientState.ToString() == "Joined");
            title.SetActive(false);
            load.SetActive(true);
            multi = StartCoroutine(this.Load());
        }
    }
    //シーンロードと相手のロード状況の同期
    private IEnumerator Load()
    {
        print("読み込み中");
        yield return new WaitUntil(() => isJoin == true);
        print("読み込み完了");
        while (true)
        {
            yield return null;
            //読み込み完了
            if (async.progress >= 0.9f)//ロード完了
            {
                if (!IsMaster)//マスターでなければ
                {
                    view.RPC("ClientComplete", RpcTarget.MasterClient);//マスターに対してロード完了の通信をする
                }
                else//マスターだったら
                {
                    yield return new WaitUntil(() => isClientLoadCompleted == peopleNuumber - 1);//他プレイヤーのロード完了を待つ
                    LoadDone();//全プレイヤーのisLoadをtrueにする
                }
                yield return new WaitUntil(() => isLoad == true);//サーバー経由のシーン繊維開始待ち
                //シーン読み込み時処理
                bgmController.SetBGMData();
                async.allowSceneActivation = true;//シーン読み込み
                break;
            }
        }
    }
    [PunRPC]
    private void ClientComplete()
    {
        isClientLoadCompleted++;
    }
    private void LoadDone()//全プレイヤーのロード終了後
    {
        view.RPC("LoadTrue", RpcTarget.AllViaServer);//自分を含むプレイヤーに対して通信を介して
    }
    [PunRPC]
    private void LoadTrue()
    {
        isLoad = true;
    }

    public void SoloDebug()
    {
        if (connect)
            StartCoroutine(Load());
        else
        {
            Debug.LogError("ルームに入室してください");
        }
        IEnumerator Load()
        {
            yield return new WaitUntil(() => PhotonNetwork.NetworkClientState.ToString() == "Joined");
            SceneManager.LoadScene("BattleScene");
        }
    }
    private void OnGUI()
    {
        //GUILayout.Label(PhotonNetwork.NetworkClientState.ToString());//サーバーへのアクセス状況
    }
    //接続
    public void Connect()
    {
        if (!connect)//接続していなければ
        {
            StartCoroutine(Connect());//マスターサーバーへのアクセスチェック
        }
        IEnumerator Connect()
        {
            yield return new WaitUntil(() => IsMasterServerConnected);//マスターサーバーにアクセスするまで待機
            string roomName = "12345";
            PhotonNetwork.JoinOrCreateRoom(roomName, new RoomOptions(), TypedLobby.Default);//ルームがあれば接続なければ生成
            print("Connect");
            connect = true;//接続へ変更
        }
    }
    //切断
    public void DisConnect()
    {
        if (connect)//接続していたら
        {
            PhotonNetwork.LeaveRoom();
            connect = false;
            print("DisConect");
        }
    }
    public override void OnConnectedToMaster()
    {
        print("マスターサーバーにアクセス");
        IsMasterServerConnected = true;
    }//マスターサーバーにアクセスしているか
    public override void OnJoinedRoom()
    {
        Room myRoom = PhotonNetwork.CurrentRoom;
        Player player = PhotonNetwork.LocalPlayer;
        print("join");
        print($"MyNumber : {player.ActorNumber}");
        print("ルームマスター : " + player.IsMasterClient);
        playerNumber = player.ActorNumber;
        IsMaster = player.IsMasterClient;
        PhotonNetwork.NickName = name_text.text;
        isJoin = true;
    }
}