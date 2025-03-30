using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class Client2 : MonoBehaviour
{
    private static Socket TCPClient2 = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    private static Socket UDPClient2 = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
    private static byte[] TCPBuffer = new byte[1024];
    private static byte[] UDPBuffer = new byte[1024];
    private static EndPoint serverEndPoint;
    private static Vector3 lastPositionClient2;

    public static GameObject Client1Cube;
    public static GameObject Client2Cube;
    public static float networkUpdateRate = 0.1f;
    private static float nextUpdateTime = 0f;

    public InputField chatInput;
    public InputField serverIPInput;
    public Text chatText;
    public static string msg;
    public GameObject popup;
    public static bool SendPos = false;
    public Button sendButton;
    public Button connectButton;

    private static Queue<Vector3> positionQueue = new Queue<Vector3>(); //Queue for the positions 

    //For Dead Reckoning 
    private static Vector3 predictedPosClient1;
    private static Vector3 veloClient1;
    private static float lastRecTimeClient1;

    private void Start()
    {
        Client1Cube = GameObject.Find("Client1Cube");
        Client2Cube = GameObject.Find("Client2Cube");

        popup.SetActive(true);

        connectButton.onClick.AddListener(ConnectToServer);

        sendButton.onClick.AddListener(SendChatMessageFromUI);
    }

    //Connects to server once connect button is clicked
    private void ConnectToServer()
    {
        string ipAddress = serverIPInput.text;

        if (IPAddress.TryParse(ipAddress, out IPAddress serverIP))
        {
            Debug.Log("Attempting to connect to server at IP: " + ipAddress);

            StartClient(serverIP);
            popup.SetActive(false);
        }
    }

    public static void StartClient(IPAddress serverIP)
    {
        TCPClient2.Connect(serverIP, 8888);
        Debug.Log("Client 2 TCP Connected To Server Using Port 8888");

        UDPClient2.Bind(new IPEndPoint(IPAddress.Any, 0)); // Bind to any available port

        serverEndPoint = new IPEndPoint(serverIP, 8889);
        Debug.Log("Client 2 UDP Connected To Server Using Port 8889");

        TCPClient2.BeginReceive(TCPBuffer, 0, TCPBuffer.Length, 0, new AsyncCallback(ReceiveTCPCallback), TCPClient2);
        UDPClient2.BeginReceiveFrom(UDPBuffer, 0, UDPBuffer.Length, 0, ref serverEndPoint, new AsyncCallback(ReceiveUDPCallback), UDPClient2);

        //Error Prevention so the client doesn't send position right when it connects. 
        SendPos = true;
    }

    void Update()
    {
        if (Time.time >= nextUpdateTime)
        {
            nextUpdateTime = Time.time + networkUpdateRate;
            if (SendPos == true)
            {
                SendPositionIfMoved();
            }

        }

        if (positionQueue.Count > 0)
        {
            Vector3 newPos = positionQueue.Dequeue();

            if (Client1Cube != null)
            {
                Client1Cube.transform.position = newPos;
                Debug.Log("Updated Client 1 Cube's position: " + Client1Cube.transform.position);
            }
        }

        chatText.text = msg;
    }

    //Chat: 
    private static void ReceiveTCPCallback(IAsyncResult result)
    {
        Socket socket = result.AsyncState as Socket;
        int rec = socket.EndReceive(result);

        string message = Encoding.ASCII.GetString(TCPBuffer, 0, rec);
        msg = message;
        Debug.Log("Received Chat Message From Client 1: " + message);

        socket.BeginReceive(TCPBuffer, 0, TCPBuffer.Length, 0, new AsyncCallback(ReceiveTCPCallback), socket);
    }

    private static void SendTCPCallback(IAsyncResult result)
    {
        TCPClient2.EndSend(result);
    }

    public void SendChatMessageFromUI()
    {
        string message = chatInput.text;

        //Checks if TCP Client is Valid
        if (TCPClient2 != null && TCPClient2.Connected)
        {
            //If message is quit it will close the connections and editor or applicaiton 
            if (message.ToLower() == "quit")
            {
                Debug.Log("Received 'quit' message from server. Stopping game.");

                CloseConnections();

            #if UNITY_EDITOR
                EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
            }

            if (!string.IsNullOrEmpty(message))
            {
                byte[] chatMessageBytes = Encoding.ASCII.GetBytes(message);
                try
                {
                    TCPClient2.BeginSend(chatMessageBytes, 0, chatMessageBytes.Length, 0, new AsyncCallback(SendTCPCallback), TCPClient2);
                }
                catch (ObjectDisposedException) //https://learn.microsoft.com/en-us/dotnet/api/system.objectdisposedexception?view=net-9.0
                {
                    Debug.LogWarning("Socket has been disposed. Cannot send message.");
                }
                chatInput.text = "";
                chatInput.ActivateInputField();
            }
        }
    
        else
        {
            Debug.LogWarning("TCP Socket is not connected. Cannot send message.");
        }
    }

    //Position and Velocity Updates: 
    private void SendPositionIfMoved()
    {
        Vector3 currentPos = Client2Cube.transform.position;

        if (currentPos != lastPositionClient2)
        {
            Vector3 currentVelo = (currentPos - lastPositionClient2) / networkUpdateRate;
            lastPositionClient2 = currentPos;
            byte[] data = new byte[24];
            //https://learn.microsoft.com/en-us/dotnet/api/system.bitconverter.getbytes?view=net-9.0

            Buffer.BlockCopy(BitConverter.GetBytes(currentPos.x), 0, data, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(currentPos.y), 0, data, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(currentPos.z), 0, data, 8, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(currentVelo.x), 0, data, 12, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(currentVelo.y), 0, data, 16, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(currentVelo.z), 0, data, 20, 4);

            UDPClient2.SendTo(data, serverEndPoint);
            Debug.Log("Client 2 Sent Position: " + currentPos + " and Velocity: " + currentVelo + " To the Server");
        }
    }

    private static void ReceiveUDPCallback(IAsyncResult result)
    {
        int rec = UDPClient2.EndReceiveFrom(result, ref serverEndPoint);

        string message = Encoding.ASCII.GetString(UDPBuffer, 0, rec);
        Debug.Log("Received UDP Message: " + message);

        string[] data = message.Split(',');

        if (data[0] == "coin_spawn" && data.Length == 4)
        {
            float x = float.Parse(data[1]);
            float y = float.Parse(data[2]);
            float z = float.Parse(data[3]);

            UnityMainThreadDispatcher.Enqueue(() => SpawnCoin(new Vector3(x, y, z)));
        }
        else if (data[0] == "coin_collected" && data.Length == 4)
        {
            float x = float.Parse(data[1]);
            float y = float.Parse(data[2]);
            float z = float.Parse(data[3]);

            UnityMainThreadDispatcher.Enqueue(() => RemoveCoin(new Vector3(x, y, z)));
        }

        // Continue receiving data
        UDPClient2.BeginReceiveFrom(UDPBuffer, 0, UDPBuffer.Length, 0, ref serverEndPoint, new AsyncCallback(ReceiveUDPCallback), UDPClient2);

        if (rec > 0)
        {
            float[] pos = new float[rec / 4];
            Buffer.BlockCopy(UDPBuffer, 0, pos, 0, rec);

            Vector3 recPos = new Vector3(pos[0], pos[1], pos[2]);
            Vector3 recVelo = new Vector3(pos[3], pos[4], pos[5]);

            Debug.Log("Received Position: " + recPos + " and Velocity: " + recVelo);

            //To use the main thread (mainly for time calculation) 
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                //Calculate time difference 
                float timeDelta = Time.time - lastRecTimeClient1;
                lastRecTimeClient1 = Time.time;

                //Predict Position
                predictedPosClient1 = recPos + recVelo * timeDelta;
                veloClient1 = recVelo;

                positionQueue.Enqueue(predictedPosClient1);
                Debug.Log("Predicted Position: " + predictedPosClient1 + " and Velocity: " + veloClient1);
            });
        }

        //Continue receiving data
        UDPClient2.BeginReceiveFrom(UDPBuffer, 0, UDPBuffer.Length, 0, ref serverEndPoint, new AsyncCallback(ReceiveUDPCallback), UDPClient2);
    }

    public static void SpawnCoin(Vector3 position)
    {
        GameObject coinPrefab = Resources.Load<GameObject>("CoinPrefab"); // Ensure you have this prefab in a "Resources" folder
        GameObject coin = Instantiate(coinPrefab, position, Quaternion.identity);
        coin.tag = "Coin";
    }

    public static void RemoveCoin(Vector3 position)
    {
        GameObject[] coins = GameObject.FindGameObjectsWithTag("Coin");
        foreach (GameObject coin in coins)
        {
            if (Vector3.Distance(coin.transform.position, position) < 0.1f)
            {
                Destroy(coin);
                break;
            }
        }
    }

    private void OnApplicationQuit()
    {
        CloseConnections();
    }

    //Close both TCP and UDP connections
    private void CloseConnections()
    {
        if (TCPClient2 != null && TCPClient2.Connected)
        {
            TCPClient2.Close();
            Debug.Log("TCP Connection Closed");
        }

        if (UDPClient2 != null)
        {
            UDPClient2.Close();
            Debug.Log("UDP Connection Closed");
        }
    }
}
