using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System;
using UnityEditor;


public class cube : MonoBehaviour
{
    public static float client2Score = 0;
    private static Socket UDPClient1;
    private static Socket TCPClient1;
    private static EndPoint serverEndPoint;

    // Start is called before the first frame update
    void Start()
    {

        UDPClient1 = Client2.UDPClient2;
        TCPClient1 = Client2.TCPClient2;

        string ipAddress = "127.0.0.1";

        if (IPAddress.TryParse(ipAddress, out IPAddress serverIP))
        {
            serverEndPoint = new IPEndPoint(serverIP, 8889);
            Debug.Log($"testing {serverEndPoint}");
        }

        // This makes sure the client is connected to the server so the server can recieve the info from the clients
    }

    // Update is called once per frame
    void Update()
    {
        transform.Translate(Input.GetAxis("Horizontal") * Time.deltaTime * 2f,
            0, Input.GetAxis("Vertical") * Time.deltaTime * 2f);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Coin")) // Check if the collided object is a coin
        {
            string message = $"coin_collected,{other.transform.position.x},{other.transform.position.y},{other.transform.position.z}";
            client2Score += 10;
            byte[] data = Encoding.ASCII.GetBytes(message);
            byte[] scoreData = Encoding.ASCII.GetBytes($"Score: {client2Score}");

            // Send the collected coin message to the server
            UDPClient1.SendTo(data, serverEndPoint);
            // Destroys the coin
            Destroy(other.gameObject);
        }
    }
}
