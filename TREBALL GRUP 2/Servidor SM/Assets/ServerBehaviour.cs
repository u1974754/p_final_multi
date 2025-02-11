using UnityEngine;
using Unity.Collections;
using Unity.Networking.Transport;
using TMPro;

namespace Unity.Networking.Transport.Samples
{
    public class ServerBehaviour : MonoBehaviour
    {
        public TextMeshProUGUI serverIpText;
        NetworkDriver m_Driver;
        NativeList<NetworkConnection> m_Connections;
        NetworkPipeline m_ReliablePipeline; // Pipeline fiable
        string serverName = "MyServer";

        private bool[] charactersSelected = new bool[3] { false, false, false };
        

        void Start()
        {   
            m_Driver = NetworkDriver.Create();
            
            m_ReliablePipeline = m_Driver.CreatePipeline(
                typeof(UnreliableSequencedPipelineStage), 
                typeof(ReliableSequencedPipelineStage)
            );

            m_Connections = new NativeList<NetworkConnection>(16, Allocator.Persistent);

            var endpoint = NetworkEndpoint.AnyIpv4.WithPort(7777);
            if (m_Driver.Bind(endpoint) != 0)
            {
                Debug.LogError("Failed to bind to port 7777.");
                return;
            }
            m_Driver.Listen();

            string localIP = GetLocalIPAddress();

            Debug.Log($"Server listening on IP {localIP}, port 7777.");

            if (serverIpText != null)
            {
                serverIpText.text = $"Server IP: {localIP}\nPort: 7777";
            }
            else
            {
                Debug.LogError("Server IP Text is not assigned in the Inspector.");
            }
        }

        void OnDestroy()
        {
            if (m_Driver.IsCreated)
            {
                m_Driver.Dispose();
                m_Connections.Dispose();
            }
        }

        void Update()
        {
            m_Driver.ScheduleUpdate().Complete();

            // Clean up connections.
            for (int i = 0; i < m_Connections.Length; i++)
            {
                if (!m_Connections[i].IsCreated)
                {
                    m_Connections.RemoveAtSwapBack(i);
                    i--;
                }
            }

            // Accept new connections.
            NetworkConnection connection;
            while ((connection = m_Driver.Accept()) != default)
            {
                m_Connections.Add(connection);
                Debug.Log("Accepted a connection.");

                SendInitialDataToClient(connection);
            }

            for (int i = 0; i < m_Connections.Length; i++)
            {
                DataStreamReader stream;
                NetworkEvent.Type cmd;
                while ((cmd = m_Driver.PopEventForConnection(m_Connections[i], out stream)) != NetworkEvent.Type.Empty)
                {
                    if (cmd == NetworkEvent.Type.Data)
                    {
                        byte messageType = stream.ReadByte();  // El índice del personaje elegido por el cliente
                        
                        if (messageType == ((byte)'S')){
                            byte characterIndex = stream.ReadByte();
                            HandleCharacterSelection(i, m_Connections[i], characterIndex);
                        }
                        else if (messageType == ((byte)'P')){
                            byte characterIndex = stream.ReadByte();
                            CheckCharacterPosition(stream, m_Connections[i], characterIndex);
                        }
                    }
                    else if (cmd == NetworkEvent.Type.Disconnect)
                    {
                        Debug.Log($"Client {i} disconnected.");
                        m_Connections[i] = default;
                        break;
                    }
                }
            }
        
        }

        private string GetLocalIPAddress()
        {
            var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            return "No IP found";
        }

        private void SendInitialDataToClient(NetworkConnection connection)
        {
            string clientName = $"Client_{m_Connections.Length - 1}";
            string previousClientName = m_Connections.Length > 1 ? $"Client_{m_Connections.Length - 2}" : "None";

            m_Driver.BeginSend(m_ReliablePipeline, connection, out var writer);

            writer.WriteByte((byte)'H'); // Header
            writer.WriteFixedString32(serverName); // Server name
            writer.WriteFixedString32(clientName);
            writer.WriteFixedString32(previousClientName);
            writer.WriteFloat(Time.time);

            m_Driver.EndSend(writer);

            Debug.Log($"Sent data to {clientName}: [Server={serverName}, Previous={previousClientName}, Time={Time.time}]");
        }

            // Manejar la selección de personaje por parte de un cliente
        private void HandleCharacterSelection(int clientIndex, NetworkConnection connection, byte characterIndex)
        {
            if (characterIndex >= 0 && characterIndex < charactersSelected.Length)
            {
                if (!charactersSelected[characterIndex])
                {
                    // El personaje está disponible, marcarlo como seleccionado
                    charactersSelected[characterIndex] = true;

                    // Enviar una confirmación al cliente
                    Debug.Log($"Cliente {clientIndex} seleccionó el personaje {characterIndex + 1}");
                    SendCharacterSelectionResponse(clientIndex, (byte)'S', "SUC", characterIndex);
                    AssignPlayerPosition(connection, characterIndex);
                }
                else
                {
                    // El personaje ya ha sido seleccionado, enviar un error al cliente
                    Debug.Log($"Personaje {characterIndex + 1} ya ha sido seleccionado");
                    SendCharacterSelectionResponse(clientIndex, (byte)'E', "ERR", characterIndex);
                }
            }
            else
            {
                Debug.LogWarning($"Invalid character index: {characterIndex}");
                SendCharacterSelectionResponse(clientIndex, (byte)'E', "ERR2", characterIndex);
            }
        }

        private void SendCharacterSelectionResponse(int clientIndex, byte responseCode, string message, byte characterIndex)
        {
            m_Driver.BeginSend(m_ReliablePipeline, m_Connections[clientIndex], out var dataStream);

            // Escribe el código de respuesta y el mensaje
            if(responseCode == ((byte)'E')){
                dataStream.WriteByte(responseCode); // 'E' para error
                dataStream.WriteFixedString32(message);  // El mensaje que el cliente recibirá
            }
            else{
                dataStream.WriteByte(responseCode);
                dataStream.WriteByte(characterIndex);
            } 
                

            // Finaliza el envío de datos
            m_Driver.EndSend(dataStream);
            Debug.Log($"Sent response to client {clientIndex}: {message}");
        }

        private void AssignPlayerPosition(NetworkConnection connection, int characterIndex)
        {
            Vector3 position = new Vector3(Random.Range(-10f, 9f), -2f, 0f);
            SendCharacterPosition(connection, characterIndex, position);
        }

        private void SendCharacterPosition(NetworkConnection connection, int characterIndex, Vector3 position)
        {
            m_Driver.BeginSend(m_ReliablePipeline, connection, out var writer);

            writer.WriteByte((byte)'P'); // Position message type
            writer.WriteByte((byte)characterIndex);
            writer.WriteFloat(position.x);
            writer.WriteFloat(position.y);
            writer.WriteFloat(position.z);

            m_Driver.EndSend(writer);
            Debug.Log($"Sent position for character {characterIndex}: {position}");
        }

        private void CheckCharacterPosition(Unity.Collections.DataStreamReader stream, NetworkConnection connection, byte characterIndex)
        {
            float posX = stream.ReadFloat();
            float posY = stream.ReadFloat();
            float posZ = stream.ReadFloat();
            
            if ((posX >= -10f && posX <= 9f) && (posY > -4f)) SendCheckProve(connection, characterIndex);
        }

        private void SendCheckProve(NetworkConnection connection, byte characterIndex)
        {
            m_Driver.BeginSend(m_ReliablePipeline, connection, out var writer);

            writer.WriteByte((byte)'B');
            writer.WriteByte((byte)characterIndex);

            m_Driver.EndSend(writer);
        }

    }
}