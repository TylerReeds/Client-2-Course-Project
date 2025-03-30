using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net.Sockets;
using System.Net;
using System;
using System.Text;

public class ClientCoinManager : MonoBehaviour
{
    public GameObject coinPrefab; // Assign this in the Inspector

    private static Socket UDPClient2;
    private static EndPoint serverEndPoint;
    private static byte[] UDPBuffer = new byte[1024];

    void Start()
    {
        UDPClient2 = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        UDPClient2.Bind(new IPEndPoint(IPAddress.Any, 8889)); // Assuming 8889 for UDP

        // Start receiving coin data from the server
        UDPClient2.BeginReceiveFrom(UDPBuffer, 0, UDPBuffer.Length, 0, ref serverEndPoint, new AsyncCallback(ReceiveUDPCallback), UDPClient2);
    }

    private void ReceiveUDPCallback(IAsyncResult result)
    {
        int receivedDataLength = UDPClient2.EndReceiveFrom(result, ref serverEndPoint);

        if (receivedDataLength > 0)
        {
            string message = Encoding.ASCII.GetString(UDPBuffer, 0, receivedDataLength);
            string[] messageParts = message.Split(',');

            if (messageParts[0] == "coin_spawn")
            {
                float x = float.Parse(messageParts[1]);
                float y = float.Parse(messageParts[2]);
                float z = float.Parse(messageParts[3]);

                Vector3 spawnPosition = new Vector3(x, y, z);

                // Instantiate the coin prefab at the received position
                InstantiateCoin(spawnPosition);
            }
        }

        // Continue receiving data
        UDPClient2.BeginReceiveFrom(UDPBuffer, 0, UDPBuffer.Length, 0, ref serverEndPoint, new AsyncCallback(ReceiveUDPCallback), UDPClient2);
    }

    public void InstantiateCoin(Vector3 position)
    {
        // Create a new coin at the specified position
        if (coinPrefab != null)
        {
            Instantiate(coinPrefab, position, Quaternion.identity);
            Debug.Log($"Coin spawned at: {position}");
        }
        else
        {
            Debug.LogError("Coin Prefab is not assigned!");
        }
    }
}
