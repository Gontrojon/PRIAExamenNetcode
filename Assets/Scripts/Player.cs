using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class Player : NetworkBehaviour
{
    // variable network para almacenar el color del player
    public NetworkVariable<Color> ColorPlayer;
    public NetworkVariable<int> MyTeam;

    // lista con los materiales de los colores que puede ser el player segun el Team
    public List<Color> playerColorsTeam1;
    public List<Color> playerColorsTeam2;
    // color que se asigna a los que no tienen team
    public Color playerColorSinTeam;
    // variable para guardar el equipo al que pertenece 0 = sin equipo, 1= equipo 1, 2 = equipo 2
    private const int SIN_TEAM_ID = 0;
    private const int TEAM1_ID = 1;
    private const int TEAM2_ID = 2;
    public int oldTeam;

    // lista que se usara para clonar y saber que colores disponibles hay
    private List<Color> disponibles;
    // renderrer para poder cambiar el color al player
    private Renderer rend;
    // variable que controla si se pueden mover
    private bool canMove;

    // velocidad de desplazamiento
    private float speed = 5f;

    // variables para controlar los tamaños de teams
    private static int team1Size = 0;
    private static int team2Size = 0;
    private const int MAX_TEAM_SIZE = 1;

    private void Awake()
    {
        rend = GetComponent<Renderer>();
        canMove = true;
        oldTeam = 0;
    }

    public override void OnNetworkSpawn()
    {
        ColorPlayer.OnValueChanged += OnPlayerColorChanged;
        MyTeam.OnValueChanged += OnMyTeamChanged;

        if (IsOwner)
        {
            MoverAlInicioServerRpc();
            MyTeam.Value = 0;
        }

        if (!IsOwner)
        {
            rend.material.color = ColorPlayer.Value;
        }
    }

    public override void OnNetworkDespawn()
    {
        ColorPlayer.OnValueChanged -= OnPlayerColorChanged;
        MyTeam.OnValueChanged -= OnMyTeamChanged;
    }

    private void OnPlayerColorChanged(Color previousValue, Color newValue)
    {
        rend.material.color = newValue;
    }

    private void OnMyTeamChanged(int previousValue, int newValue)
    {
        oldTeam = previousValue;
    }


    [ServerRpc]
    public void MoverAlInicioServerRpc(ServerRpcParams rpcParams = default)
    {
        if (NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(rpcParams.Receive.SenderClientId).GetComponent<Player>().canMove)
        {
            transform.position = GetRandomInicioPosition();

        }

    }

    [ServerRpc]
    void MoveServerRpc(Vector3 movement, ServerRpcParams serverRpcParams = default)
    {
        // se mueve el transform acorde al movimiento que nos manda el cliente;
        transform.position += movement;
    }

    [ServerRpc]
    void ColorEquipoServerRpc(int teamid, ServerRpcParams rpcParams = default)
    {
        MyTeam.Value = teamid;
        Debug.Log("mi equipoe es : " + MyTeam.Value);
        // se usa un switch para capturar los casos y dependiendo del team al que pertenece se hace la llamada al metodo de equipo correspondiente
        switch (MyTeam.Value)
        {
            case 0:
                Debug.Log("se llama a sin equipo");
                Sinequipo(rpcParams.Receive.SenderClientId);
                break;
            case 1:
                Debug.Log("se llama a equipo 1");
                Equipo1();
                break;
            case 2:
                Debug.Log("se llama a equipo 2");
                Equipo2();
                break;
        }


    }

    void Sinequipo(ulong clientid)
    {
        // se asigna el color sin equipo
        ColorPlayer.Value = playerColorSinTeam;

        int oldTeam = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientid).GetComponent<Player>().oldTeam;
        // se comprueba al equipo que pertenece para restarlo
        Debug.Log("funcion sin equipo mi equipo es: " + MyTeam.Value);
        Debug.Log("mi antiguo equipo es: " + oldTeam);
        if (oldTeam == TEAM1_ID)
        {
            Debug.Log("Se resta en el equipo 1");
            team1Size--;
        }
        else if (oldTeam == TEAM2_ID)
        {
            Debug.Log("Se resta en el equipo 2");
            team2Size--;
        }
        // si en la resta ya no hay el numeor maximo en algun equipo se libera el movmiento
        if (team1Size < MAX_TEAM_SIZE || team2Size < MAX_TEAM_SIZE)
        {
            CanMoveFreeClientRpc();
        }
        // se le asigna que pertece a sin equipo
        MyTeam.Value = 0;
    }

    void Equipo1()
    {
        // se suma 1 al tamaño del equipo
        team1Size++;
        // se le asigna un color aleatorio de los de su equipo
        ColorPlayer.Value = ColorDisponibleLista(playerColorsTeam1);
        // si se alcanza el maximo de miembros de equipo se restringe el movimiento
        if (team1Size == MAX_TEAM_SIZE)
        {
            RestrictMovement(TEAM1_ID);
        }
    }

    void Equipo2()
    {
        // se suma 1 al tamaño del equipo
        team2Size++;
        // se le asigna un color aleatorio de los de su equipo
        ColorPlayer.Value = ColorDisponibleLista(playerColorsTeam2);
        // si se alcanza el maximo de miembros de equipo se restringe el movimiento
        if (team2Size == MAX_TEAM_SIZE)
        {
            RestrictMovement(TEAM2_ID);
        }
    }

    void RestrictMovement(int team)
    {
        Debug.Log("se alcanzo tamaño maximo de jugadores");
        // creacion de parametros para que solo los del equipo se puedan mover
        ClientRpcParams clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = GetIdTeams(team)
            }
        };
        if (clientRpcParams.Send.TargetClientIds == null)
        {
            Debug.Log("el contenido es null y no se envian ids");
        }

        Debug.Log("Se procede a realizar el clientrpc");
        CanMoveRestrictClientRpc(clientRpcParams);
    }

    List<ulong> GetIdTeams(int team)
    {
        List<ulong> teamids = new List<ulong>();
        foreach (ulong uid in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(uid).GetComponent<Player>().MyTeam.Value == team)
            {
                Debug.Log("se añadio el id: " + uid);
                teamids.Add(uid);
            }

        }
        return teamids;
    }

    [ClientRpc]
    void CanMoveFreeClientRpc()
    {
        // se deja movimiento libre
        canMove = true;
    }

    [ClientRpc]
    void CanMoveRestrictClientRpc(ClientRpcParams clientRpcParams = default)
    {
        //Debug.Log(clientRpcParams.Send.TargetClientIds[0]);
        if (clientRpcParams.Send.TargetClientIds.Count == 0)
        {
            Debug.Log(" el contenido de los clientRpcParams es NULL *********************************");
            return;
        }
        /*
        foreach (ulong ide in clientRpcParams.Send.TargetClientIds)
        {
            Debug.Log("bulce de movimiento, mi ide es: " + OwnerClientId + " los ide de clientes que se mueven " + ide);
            if (ide != OwnerClientId)
            {
                Debug.Log("no se mueve");
                canMove = false;
            }else if (ide == OwnerClientId)
            {
                Debug.Log("se mueve");
                canMove = true;
            }
        }*/
        
        List<ulong> u = (List<ulong>)clientRpcParams.Send.TargetClientIds;
        Debug.Log("id de la posicion 0 es: " + u[0]);
        if (!u.Contains(OwnerClientId))
        {
            canMove = false;
        }

    }

    public void Mover()
    {
        transform.position = GetRandomInicioPosition();
    }

    private Color ColorDisponibleLista(List<Color> playerColorsTeam)
    {
        // lista para guardar los colores de los clientes conectados
        List<Color> coloresUsados = new List<Color>();
        // se recorre los clientes conectados para guardar los colores que estan usando
        foreach (ulong uid in NetworkManager.Singleton.ConnectedClientsIds)
        {
            // se añade a la lista
            coloresUsados.Add(NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(uid).GetComponent<Player>().ColorPlayer.Value);
        }
        // se hace un clon de la lista completa de colores
        disponibles = new List<Color>(playerColorsTeam);
        // Se elimina los colores que se han usado
        disponibles.RemoveAll(coloresUsados.Contains);
        /* 
         * Como esta parametrizado el tamaño de los equipos y solo hay 3 colores
         * si se asigna un numero mayor a 3 al tamño de equipo el restante seran siempre negro
         * en caso de que no se quiera este comportamiento habria que añadir mas colores en el prefab del player
         */
        if (disponibles.Count == 0)
        {
            return Color.black;
        }
        // se devuelve un color aleatorio de los disponibles
        return disponibles[Random.Range(0, disponibles.Count)];
    }

    static Vector3 GetRandomInicioPosition()
    {
        // se genera una posicion aleatoria dentro de los margenes del plano sin equipo
        return new Vector3(Random.Range(-3f, 3f), 1f, Random.Range(-3f, 3f));
    }

    void Update()
    {
        // si eres owner mueve con los imputs
        if (IsOwner && canMove)
        {
            // se llama al server RPC para que efectue el movimiento dle personaje con los calculos ya hechos de speed y time.deltatime
            MoveServerRpc(new Vector3(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical")) * speed * Time.deltaTime);
            // si el usuario pulsa la tecla "M" asociazada al input RestartPosition se le movera a una posicion random del inicio
            if (Input.GetButtonDown("RestartPosition"))
            {
                MoverAlInicioServerRpc();
            }

        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (IsOwner)
        {
            //Dependiendo del collider con el que entre en colision el usuario se llama al serverpc pasando el equipo al que pertenece
            if (collision.gameObject.CompareTag("SinEquipo"))
            {
                Debug.Log("sin equipo");
                //MyTeam.Value = SIN_TEAM_ID;
                ColorEquipoServerRpc(SIN_TEAM_ID);
            }

            if (collision.gameObject.CompareTag("Equipo1"))
            {
                Debug.Log(" equipo 1");
                //MyTeam.Value = TEAM1_ID;
                ColorEquipoServerRpc(TEAM1_ID);
            }

            if (collision.gameObject.CompareTag("Equipo2"))
            {
                Debug.Log(" equipo 2");
                //MyTeam.Value = TEAM2_ID;
                ColorEquipoServerRpc(TEAM2_ID);
            }
        }
    }
}
