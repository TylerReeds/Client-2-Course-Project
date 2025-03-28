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

        //Processes Position Queue to move the cube
        if (positionQueue.Count > 0)
        {
            Vector3 newPos = positionQueue.Dequeue();
            if (Client1Cube != null)
            {
                Client1Cube.transform.position = newPos; //Updates Cube position
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

    //Position Updates
    private void SendPositionIfMoved()
    {
        Vector3 currentPos = Client2Cube.transform.position;
        if (currentPos != lastPositionClient2)
        {
            lastPositionClient2 = currentPos;
            byte[] posData = new byte[12];
            //https://learn.microsoft.com/en-us/dotnet/api/system.bitconverter.getbytes?view=net-9.0
            Buffer.BlockCopy(BitConverter.GetBytes(currentPos.x), 0, posData, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(currentPos.y), 0, posData, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(currentPos.z), 0, posData, 8, 4);

            UDPClient2.SendTo(posData, serverEndPoint);
            Debug.Log("Client 2 Sent Position To Server:" + currentPos);
        }
    }

    private static void ReceiveUDPCallback(IAsyncResult result)
    {
        int rec = UDPClient2.EndReceiveFrom(result, ref serverEndPoint);

        float[] pos = new float[rec / 4];
        Buffer.BlockCopy(UDPBuffer, 0, pos, 0, rec);

        Vector3 recPos = new Vector3(pos[0], pos[1], pos[2]);
        positionQueue.Enqueue(recPos);
        Debug.Log("Client 2 Received Position from Client 1: " + recPos);

        //Continue receiving data
        UDPClient2.BeginReceiveFrom(UDPBuffer, 0, UDPBuffer.Length, 0, ref serverEndPoint, new AsyncCallback(ReceiveUDPCallback), UDPClient2);
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
