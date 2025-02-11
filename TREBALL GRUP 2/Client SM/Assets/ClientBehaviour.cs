using UnityEngine;
using Unity.Networking.Transport;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

namespace Unity.Networking.Transport.Samples
{
    public class ClientBehaviour : MonoBehaviour
    {
        public TMP_InputField ipAddressInput; // Campo para escribir la IP
        public TMP_InputField portInput; // Campo para escribir el puerto
        public Button connectButton; // Botón para conectarse
        public TextMeshProUGUI statusText; // Texto para mostrar el estado

        public NetworkDriver m_Driver;
        public NetworkConnection m_Connection;
        public NetworkPipeline m_ReliablePipeline;

        public Button characterButton1;
        public Button characterButton2;
        public Button characterButton3;
        
        private string[] availableCharacters = new string[] { "Character1", "Character2", "Character3" };

        public GameObject[] characterPrefabs;

        public GameObject player;
        public PlayerMovement playerScript;

        void Start()
        {
            DontDestroyOnLoad(gameObject);
            
            m_Driver = NetworkDriver.Create();

            m_ReliablePipeline = m_Driver.CreatePipeline(
                typeof(UnreliableSequencedPipelineStage), 
                typeof(ReliableSequencedPipelineStage)
            );

            connectButton.onClick.AddListener(ConnectToServer);

            statusText.text = "Introduce IP y puerto para conectarte.";
        }

        void OnDestroy()
        {
                m_Driver.Dispose();
        }

        void Update()
        {
            m_Driver.ScheduleUpdate().Complete();

            if (!m_Connection.IsCreated)
            {
                return;
            }

            Unity.Collections.DataStreamReader stream;
            NetworkEvent.Type cmd;
            while ((cmd = m_Connection.PopEvent(m_Driver, out stream)) != NetworkEvent.Type.Empty)
            {
                if (cmd == NetworkEvent.Type.Connect)
                {
                    statusText.text = "¡Conexión establecida! Cargando escena...";
                    Debug.Log("Conectado al servidor.");

                    DontDestroyOnLoad(gameObject);

                    // Cambiar a la escena de selección de personaje
                    SceneManager.LoadScene("SeleccionPersonaje");
                }
                else if (cmd == NetworkEvent.Type.Data)
                {
                    // Llegir el missatge segons l'estructura
                    char messageType = (char)stream.ReadByte();
                    if (messageType == 'H')
                    {
                        string serverName = stream.ReadFixedString32().ToString();
                        string clientName = stream.ReadFixedString32().ToString();
                        string previousClientName = stream.ReadFixedString32().ToString();
                        float serverTime = stream.ReadFloat();

                        Debug.Log($"Rebut: Codi del missatge={messageType}, " +
                                $"Nom del Server={serverName}, " +
                                $"Nom del Client={clientName}, " +
                                $"Nom del Client Anterior={previousClientName}, " +
                                $"Temps={serverTime}");
                    }  
                    else if (messageType == ((byte)'P'))
                    {
                        Debug.Log($"CODIGO DE MENSAJE P");
                        ReceiveCharacterSpawnPosition(stream);
                    }
                    else if (messageType == ((byte)'S')){
                        Debug.Log($"CODIGO DE MENSAJE S");
                        byte characterIndex = stream.ReadByte();
                        string sceneName = GetCharacterSceneName(characterIndex);
                        SceneManager.LoadScene(sceneName);
                        player = GameObject.FindGameObjectWithTag("Player");
                        SendPlayerPosition(player, characterIndex);
                    }
                    else if (messageType == ((byte)'E')){
                        Debug.Log($"CODIGO DE MENSAJE E");
                        Debug.LogError("No se puede seleccionar el personaje.");
                    }
                    else if (messageType == ((byte)'B')){
                        Debug.Log($"CODIGO DE MENSAJE B");
                        byte characterIndex = stream.ReadByte();
                        SendPlayerPosition(player, characterIndex);
                        SendPlayerLife(player, characterIndex);
                    }
                    else if (messageType == ((byte)'M')){
                        Debug.Log($"CODIGO DE MENSAJE M");
                        byte characterIndex = stream.ReadByte();
                        YouAreHacker();
                    }
                    else if (messageType == ((byte)'A')){
                        Debug.Log($"CODIGO DE MENSAJE A");
                        byte characterIndex = stream.ReadByte();
                        //ReceiveEnemyPosition(enemyIndex);
                    }  
                }
                else if (cmd == NetworkEvent.Type.Disconnect)
                {
                    statusText.text = "Desconectado del servidor.";
                    Debug.Log("Desconectado del servidor.");
                    //m_Connection = default;
                }
            }
        }

        public void ConnectToServer()
        {
            if (string.IsNullOrEmpty(ipAddressInput.text) || string.IsNullOrEmpty(portInput.text))
            {
                statusText.text = "Introduce una IP y un puerto válidos.";
                return;
            }

            string ipAddress = ipAddressInput.text;
            ushort port = ushort.Parse(portInput.text);

            var endpoint = NetworkEndpoint.Parse(ipAddress, port);
            m_Connection = m_Driver.Connect(endpoint);

            statusText.text = "Conectando al servidor...";
            Debug.Log($"Intentando conectar a {ipAddress}:{port}");
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene SeleccionPersonaje, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            GameObject[] objetos = GameObject.FindGameObjectsWithTag("BotonPersonaje");
            List<Button> botonesPersonajes = new List<Button>();

            foreach (GameObject obj in objetos)
            {
                Button boton = obj.GetComponent<Button>();
                if (boton != null)
                {
                    botonesPersonajes.Add(boton);
                }
            }

            foreach (Button boton in botonesPersonajes)
            {
                if (boton.name == "Button1")
                {
                    characterButton1 = boton;
                }
                else if (boton.name == "Button2")
                {
                    characterButton2 = boton;
                }
                else if (boton.name == "Button3")
                {
                    characterButton3 = boton;
                }
            }
    
            Debug.Log("Botones encontrados en la nueva escena");
    
            if (characterButton1 == null || characterButton2 == null || characterButton3 == null)
            {
                Debug.Log("No se encontraron los botones de selección de personaje.");
                return;
            }
            // Agregar listeners a los botones
            characterButton1.onClick.AddListener(() => SelectCharacter(0));
            characterButton2.onClick.AddListener(() => SelectCharacter(1));
            characterButton3.onClick.AddListener(() => SelectCharacter(2));
        }

        private void OnEnable()
        {
            // Esperar a que la nueva escena se cargue completamente
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void SelectCharacter(int characterIndex)
        {
            if (!m_Connection.IsCreated)
            {
                Debug.LogError("Conexión no creada, no se puede seleccionar un personaje.");
                return;
            }

            // Enviar la selección de personaje al servidor
            if (m_Driver.IsCreated)
            {
                // Iniciar el envío de datos
                m_Driver.BeginSend(m_ReliablePipeline, m_Connection, out var dataStream);

                if (dataStream.IsCreated)
                {
                    dataStream.WriteByte((byte)'S');
                    dataStream.WriteByte((byte)characterIndex);  // Enviar el índice del personaje seleccionado
                    m_Driver.EndSend(dataStream);
                    statusText.text = $"Seleccionaste: {availableCharacters[characterIndex]}";
                    Debug.Log($"Personaje seleccionado: {availableCharacters[characterIndex]}");
                }
                else
                {
                    Debug.LogError("Error al crear el dataStream.");
                }
            }
            else
            {
                Debug.LogError("El driver o la pipeline no están correctamente inicializados.");
            }
        }

        private string GetCharacterSceneName(byte characterIndex){
           string name = "";
            switch (characterIndex)
            {
                case 0:
                    name = "Personaje1"; 
                    break;
                case 1:
                    name = "Personaje2"; 
                    break;
                case 2:
                    name = "Personaje3";
                    break;
            }
            return name;
        }

        void ReceiveCharacterSpawnPosition(Unity.Collections.DataStreamReader stream)
        {
            byte characterIndex = stream.ReadByte();
            float posX = stream.ReadFloat();
            float posY = stream.ReadFloat();
            float posZ = stream.ReadFloat();
    
            Vector3 spawnPosition = new Vector3(posX, posY, posZ);
            SpawnCharacter(characterIndex, spawnPosition);
        }

        void SpawnCharacter(int characterIndex, Vector3 spawnPosition)
        {
            
            if (characterIndex < 0 || characterIndex >= characterPrefabs.Length)
            {
                Debug.LogError("Índice de personaje inválido.");
                return;
            }

            GameObject characterPrefab = characterPrefabs[characterIndex];
            if (characterPrefab == null)
            {
                Debug.LogError("Prefab de personaje no asignado.");
                return;
            }

            Instantiate(characterPrefab, spawnPosition, Quaternion.identity);
            Debug.Log($"Personaje {characterIndex} instanciado en {spawnPosition}");
        }

        void SendPlayerPosition(GameObject player, byte characterIndex)
        {
            m_Driver.BeginSend(m_ReliablePipeline, m_Connection, out var dataStream);

            if (dataStream.IsCreated)
            {
                dataStream.WriteByte((byte)'P');
                dataStream.WriteByte((byte)characterIndex);  // Enviar el índice del personaje
                dataStream.WriteFloat(player.transform.position.x);
                dataStream.WriteFloat(player.transform.position.y);
                dataStream.WriteFloat(player.transform.position.z);
            }

            m_Driver.EndSend(dataStream);
        }

        void YouAreHacker()
        {

        }

        void SendPlayerLife(GameObject player, byte characterIndex)
        {
            m_Driver.BeginSend(m_ReliablePipeline, m_Connection, out var dataStream);

            if (dataStream.IsCreated)
            {
                dataStream.WriteByte((byte)'V');
                dataStream.WriteByte((byte)characterIndex);
                dataStream.WriteInt(playerScript.lives);
            }

            m_Driver.EndSend(dataStream);
        }

    }
}