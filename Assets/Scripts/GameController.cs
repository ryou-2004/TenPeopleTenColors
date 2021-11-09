using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;
using System.Text.RegularExpressions;

public class GameController : MonoBehaviour, IPunObservable
{
    [Header("出題中のCanvas")] public GameObject questionerScreen;//出題者
    [Header("回答中のCanvas")] public GameObject respondentScreen;//回答者
    [Header("回答中表示")] public GameObject answerButtons; //回答ボタン表示
    [Header("他者の回答待ち")] public GameObject waitAnswerScreen; //回答待ち
    [Header("出題待ちのCanvas")] public GameObject waitRespondentScreen;//回答者
    [Header("ターン事の結果")] public GameObject resultScreen;
    [Header("総合結果")] public GameObject allResultScreen;
    [Header("設定")] public GameObject settingScreen;

    [Header("問題")]
    [Header("出題者")] public InputField question_que;//質問
    [Header("選択肢")] public List<InputField> selection_que;//選択肢
    [Header("ドロップダウン")] public Dropdown dropdown_que;
    [Header("ドロップダウンの字")] public List<string> conjunctionList;

    [Header("問題")]
    [Header("回答者")] public Text question_res;
    [Header("選択肢")] public List<GameObject> selection_res;
    [Header("選択肢(親)")] public GameObject answering;
    [Header("他者の回答待ち")] public GameObject waiting;

    [Header("結果")] public List<Text> otherAnswer;
    [Header("結果(マルバツ")] public List<Text> otherAnswerShape;
    [Header("最終結果")] public List<Text> lastOtherAnswer;

    [Header("ターン数設定スライダー")]
    [Header("設定")] public Slider endTurnSlider;
    [Header("ターン数テキスト")] public Text endTurnText_owner;
    [Header("ターン数テキスト")] public Text endTurnText_gest;
    [Header("参加者一覧")] public List<Text> playerNameList_text;
    [Header("ターン数表示")] public Text turnText;
    [Header("オーナー設定画面")] public GameObject ownerSetting;
    [Header("ゲスト設定画面")] public GameObject gestSetting;

    [Header("ジャンルのText")] public Text genreText;
    [Header("ジャンル")] public List<string> genreList;
    [Header("出題者の名前")] public GameObject questionerName;
    [Header("残りターン数")] public Text lastTurnText;
    [Header("王冠(上から順に)")] public List<GameObject> crown;
    [Header("Resultの次へボタン")] public GameObject nextTurnButton;
    [Header("ReStartのやつ")] public GameObject reStartButtons;

    public BGMController bgmController;

    private PhotonView view;
    private Dictionary<int, int> answerDic = new Dictionary<int, int>();
    private int[] totalScore;
    private string[] playerNameList;
    private string[] selectionArr;
    private string genre;
    private int localNextGame;

    private bool isAllAnswer;//全員回答したか
    private int answerPeople;//回答済みの人数
    private int nextGamePeople;//次のゲームに進む人数
    private int notNextGamePeople;//次のゲームに進まない人数

    private int endTurn;//何ターン制か
    private int turn;//↓のために必要(しらんけど
    private int Turn
    {
        get { return turn; }
        set
        {
            if (value == 0) value = SimplePun.peopleNuumberSta;
            this.turn = value;
        }
    }//自分が回答者になるまでのターン
    private int elepsedTurn;//現在何ターン目か
    private void Start()
    {
        Turn = SimplePun.playerNumber;
        view = GetComponent<PhotonView>();
        GameSetting();
        playerNameList = new string[PhotonNetwork.PlayerList.Length];
        for (int i = 0; i < playerNameList.Length; i++)
        {
            playerNameList[i] = PhotonNetwork.PlayerList[i].NickName;
            playerNameList_text[i].text = playerNameList[i];
        }
    }
    public void GameSetting()//初期設定
    {
        totalScore = new int[SimplePun.peopleNuumberSta];//スコアの初期化
        elepsedTurn = 1;//現在のターン設定
        TurnSet();
        AllReset(settingScreen);//設定画面表示
        ownerSetting.SetActive(false);
        gestSetting.SetActive(false);
        if (PhotonNetwork.LocalPlayer.IsMasterClient)
            ownerSetting.SetActive(true);//マスターだったらターン設定画面表示
        else
            gestSetting.SetActive(true);//そうでなければマスターの設定を監視
        lastTurnText.gameObject.SetActive(false);//残りターンの表示消す
    }
    public void GameStart()//ゲームスタート
    {
        ownerSetting.SetActive(false);
        gestSetting.SetActive(false);
        view.RPC("GameStartSignal", RpcTarget.All);
    }
    [PunRPC]//ゲームスタート時の通信
    private void GameStartSignal()
    {
        endTurn *= SimplePun.peopleNuumberSta;//何周するかからターン数を計算
        lastTurnText.text = $"現在ターン　{elepsedTurn}/{endTurn}";//残りターンのテキスト書き換え
        lastTurnText.gameObject.SetActive(true);//残りターンのテキスト
        if (Turn == 1)//出題者
        {
            answerPeople = 0;//回答人数を初期化
            isAllAnswer = false;
            genre = genreList[UnityEngine.Random.Range(0, genreList.Count)];//ジャンルをランダムで設定
            view.RPC("FromQuestionerSignal", RpcTarget.All, PhotonNetwork.LocalPlayer.NickName, genre);//出題者の名前を送信
            AllReset(questionerScreen);//出題画面へ
        }
        else//回答者
        {
            AllReset(waitRespondentScreen);//出題待ち画面へ
        }
    }
    public void TurnSet()//ターンのスライダーを変えたときの処理
    {
        if (SimplePun.IsMaster)//オーナーのみ
        {
            endTurnText_owner.text = endTurnSlider.value + "周";//スライダーの右のテキスト変更
            endTurn = (int)endTurnSlider.value * SimplePun.peopleNuumberSta;
            view.RPC("TurnText", RpcTarget.All, (int)endTurnSlider.value);
        }
        //ゲストのテキスト変更
    }
    [PunRPC]//ターン毎に必要な情報
    private void TurnText(int i)
    {
        endTurnText_gest.text = i + "周";
        endTurn = i;
    }
    public void CompleteQuestion()//出題者側の処理 出題完了
    {
        string que = conjunctionList[dropdown_que.value] + "、" + question_que.text;
        selectionArr = selection_que.Select(x => x.text).ToArray();//InputFieldの配列をstringの配列に変換
        selection_que.Select(x => x.text = "");
        string ans = string.Join(" ", selectionArr);//一行にする
        view.RPC("QuestionSignal", RpcTarget.All, que, ans);
    }
    [PunRPC]//全員の処理 回答開始
    private void QuestionSignal(string question, string ansStr)
    {
        AllReset(respondentScreen);//回答画面へ
        question_res.text = question;//問題を反映
        selectionArr = ansStr.Split(' ').ToArray();//配列に変換
        for (int i = 0; i < selectionArr.Length; i++)
        {
            selection_res[i].GetComponentInChildren<Text>().text = selectionArr[i];//選択肢の反映
        }
    }
    public void CompleteAnswer(int answerNumber)//回答した時の処理(答えの番号)
    {
        view.RPC("RespondentWait", RpcTarget.All, SimplePun.playerNumber, answerNumber);//回答したよ～

        waiting.SetActive(true);//他者の回答待ち画面true
        answering.SetActive(false);//選択肢をfalse
        if (Turn == 1)//出題者だったら
            StartCoroutine(QuestionerWait());
        IEnumerator QuestionerWait()
        {
            yield return new WaitUntil(() => isAllAnswer == true);//全員回答するまで待機
            isAllAnswer = false;
            answerPeople = 0;
            //出題者が答え合わせ
            string[] isCorrect = new string[SimplePun.peopleNuumberSta];
            answerDic = answerDic.OrderBy(x => x.Key).ToDictionary(y => y.Key, y => y.Value);//プレイヤー番号順にする
            List<string> playerNumber = new List<string>();
            var li = Enumerable.Range(1, 3).ToList();
            li.Remove(SimplePun.playerNumber);
            isCorrect[0] = answerNumber.ToString();
            isCorrect[1] = answerDic[li[0]].ToString();
            if(answerDic.ContainsKey(li[1]))
            isCorrect[2] = answerDic[li[1]].ToString();

            for (int i = 0; i < 3; i++)
            {
                playerNumber.Add(i.ToString());
            }
            answerDic = new Dictionary<int, int>();

            string result = "";//出題者含むプレイヤーの答え
            foreach (var v in isCorrect)
                result += v + " ";

            answering.SetActive(true);//選択肢をtrue
            waiting.SetActive(false);//他者の回答待ち画面false
            bool isGameEnd;//このターンで終了するか
            if (endTurn == elepsedTurn)//現在のターンで終了するとき
                isGameEnd = true;
            else
                isGameEnd = false;
            playerNumber.Remove((SimplePun.playerNumber - 1).ToString());
            playerNumber.Insert(0, (SimplePun.playerNumber - 1).ToString());
            string plaStr = "";
            foreach (var v in playerNumber)
                plaStr += v + " ";
            view.RPC("CompleteTurn", RpcTarget.MasterClient, result, li[0] - 1, li[1] - 1);//トータススコアの加算
            view.RPC("Result", RpcTarget.All, result, plaStr, isGameEnd);//毎ターンリザルトの表示通信
        }
    }
    [PunRPC]//回答したときの出題者への通信
    private void RespondentWait(int playerNumber, int answerNumber)//ナンバー,回答
    {
        if (Turn == 1)//出題者だったら
        {
            answerPeople++;//回答人数を増やす
            answerDic.Add(playerNumber, answerNumber);//出題者の回答リストに追加
            if (answerPeople == SimplePun.peopleNuumberSta)//全員が回答したら
            {
                isAllAnswer = true;
                answerPeople = 0;
            }
        }
    }
    [PunRPC]//総合結果保持の処理(マスターに対してのみ
    private void CompleteTurn(string correct, int answer1, int answer2)//(index1=答え)
    {
        List<int> corList = correct.Trim().Split(' ').Select(int.Parse).ToList();//int型のリストに変換
        List<int> ansList = new List<int>() { answer1, answer2 };
        for (int i = 1; i < corList.Count; i++)
        {
            if (corList[0] == corList[i])
                totalScore[ansList[i - 1]] += 1;//スコアの加算
        }
    }
    [PunRPC]//ターン終了時のリザルト表示
    private void Result(string result, string answerNumber, bool gameEnd)
    {
        List<int> ansArr = result.Trim().Split(' ').Select(int.Parse).ToList();
        int answer = ansArr[0];
        List<int> playerNumber = answerNumber.Trim().Split(' ').Select(int.Parse).ToList();
        AllReset(resultScreen);//リザルトスクリーン表示
        otherAnswer[0].text = $"答えは「{selectionArr[answer]}」でした";
        for (int i = 1; i < ansArr.Count; i++)//結果を表示(出題者は含まないため i = 1 Count+1
        {
            otherAnswer[i].text = playerNameList[playerNumber[i]] + "：" + selectionArr[ansArr[i]];
            if (ansArr[i] == answer)
            {
                otherAnswerShape[i - 1].text = "〇";
            }
            else
            {
                otherAnswerShape[i - 1].text = "×";
            }
        }
        StartCoroutine(NextTurnProcess());//ターン単位の初期化
        //10秒待ってから次のターンor総合結果に遷移
        IEnumerator NextTurnProcess()
        {
            yield return new WaitUntil(() => localNextGame == SimplePun.peopleNuumberSta);//全員がボタンを押したら
            if (!gameEnd)//まだゲームが続くなら
            {
                //次のターンへの処理
                Turn--;//出題者までのターン数マイナス
                elepsedTurn++;//経過ターンプラス
                lastTurnText.text = $"現在ターン　{elepsedTurn}/{endTurn}";//ターン変更
                answering.SetActive(true);//選択肢をtrue
                waiting.SetActive(false);//他者の回答待ち画面false
                nextTurnButton.SetActive(true);
                localNextGame = 0;
                answerPeople = 0;
                isAllAnswer = false;
                question_que.text = "";//問題文初期化
                foreach (var v in selection_que)
                    v.text = "";
                if (Turn == 1)//次の出題者
                {
                    genre = genreList[UnityEngine.Random.Range(0, genreList.Count)];
                    view.RPC("FromQuestionerSignal", RpcTarget.All, PhotonNetwork.LocalPlayer.NickName, genre);//出題者の名前を送信
                    AllReset(questionerScreen);//出題画面へ
                }
            }
            else//総合結果へ自動遷移
            {
                view.RPC("MasterAllResult", RpcTarget.MasterClient);//マスターに対して総合結果の通信を要求
            }
        }
    }
    public void NextTurn()
    {
        nextTurnButton.SetActive(false);
        view.RPC("LocalNextGame", RpcTarget.All);
    }
    [PunRPC]
    private void LocalNextGame()
    {
        localNextGame++;
    }
    [PunRPC]//ゲーム終了時にマスターに対して総合結果の通信を要求
    private void MasterAllResult()
    {
        string allResult = "";
        foreach (var v in totalScore)
            allResult += v + " ";
        view.RPC("OverallResult", RpcTarget.All, allResult);
    }
    [PunRPC]//総合結果
    private void OverallResult(string allResult)//(全員のリザルト)
    {
        foreach (var i in crown) i.SetActive(false);
        List<string> allResArr = allResult.Trim().Split(' ').ToList();//ストリングを配列に変換
        int maxPoint = 0;
        List<int> winnerId = new List<int>();
        for (int i = 0; i < allResArr.Count; i++)
        {
            lastOtherAnswer[i].text = playerNameList[i] + " : " + allResArr[i] + "ポイント";//スコア表示
            int value = int.Parse(Regex.Replace(allResArr[i], @"[^0-9]", ""));
            if(maxPoint == value)
            {
                winnerId.Add(i);
                maxPoint = value;
            }
            else if(maxPoint < value)
            {
                winnerId.Clear();
                winnerId.Add(i);
                maxPoint = value;
            }
        }
        for (int i = 0; i < winnerId.Count; i++)
        {
            crown[winnerId[i]].SetActive(true);
        }
        AllReset(allResultScreen);
        //最終ターンの表示後
        //総合結果の処理
        var wait = StartCoroutine(WaitAns());
        var notWait = StartCoroutine(NotWaitAns());
        IEnumerator WaitAns()//他プレイヤーの継続可否待ち
        {
            yield return new WaitUntil(() => nextGamePeople == SimplePun.peopleNuumberSta);//人数が揃ったら
            StopCoroutine(WaitAns());
            nextGamePeople = notNextGamePeople = 0;
            bgmController.SetBGMData();
            SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().name);//MainSceneLoad
        }
        IEnumerator NotWaitAns()
        {
            yield return new WaitUntil(() => 0 < notNextGamePeople);//続ける人が一人でもいなかったら
            StopCoroutine(NotWaitAns());
            //続ける人が一人でもいないことを伝えて
            bgmController.SetBGMData();
            SceneManager.LoadSceneAsync("TitleScene");//タイトル画面に戻る
        }
    }
    [PunRPC]//出題者の名前送信
    private void FromQuestionerSignal(string name, string genre)
    {
        questionerName.GetComponentInChildren<Text>().text = name;
        genreText.text = "ジャンル：" + genre;
        isAllAnswer = false;
        answerPeople = 0;
        AllReset(waitRespondentScreen);//回答画面へ
    }
    public void ReStart(bool isReStart)//リスタート
    {
        view.RPC("NextGameSignal", RpcTarget.All, isReStart);
        reStartButtons.SetActive(false);
    }
    [PunRPC]
    private void NextGameSignal(bool isNextGame)
    {
        if (isNextGame)
        {
            nextGamePeople++;
        }
        else
        {
            notNextGamePeople++;
        }
    }
    private void AllReset(GameObject trueObj)
    {
        questionerScreen.SetActive(false);//出題画面
        respondentScreen.SetActive(false);//回答画面
        waitRespondentScreen.SetActive(false);//出題待ち画面
        resultScreen.SetActive(false);//ターン事の結果表示画面
        allResultScreen.SetActive(false);//総合結果表示画面
        settingScreen.SetActive(false);//設定画面
        questionerName.SetActive(false);//出題者の名前
        genreText.transform.parent.gameObject.SetActive(false);//ジャンルテキスト
        trueObj.SetActive(true);//必要なスクリーン表示
        if (trueObj == questionerScreen || trueObj == respondentScreen || trueObj == waitRespondentScreen)//出題中、回答中、出題待ちだったら
        {
            questionerName.SetActive(true);//出題者の名前
            genreText.transform.parent.gameObject.SetActive(true);//ジャンルテキスト
        }
    }
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
    }
}