# PachiChat TCP/UDP

### Aplicacion de mensajeria en tiempo real con soporte para TCP y UDP, desarrollada en Unity 6


[Ver Demo en Vimeo](https://vimeo.com/1172376151)

</div>

---

## Tabla de Contenidos

- [Sobre el Proyecto](#sobre-el-proyecto)
- [Caracteristicas](#caracteristicas)
- [Arquitectura](#arquitectura)
- [Instalacion](#instalacion)
- [Uso](#uso)
- [Estructura del Proyecto](#estructura-del-proyecto)
- [Flujo de la Aplicacion](#flujo-de-la-aplicacion)
- [Diferencias TCP vs UDP](#diferencias-tcp-vs-udp)
- [Interfaces](#interfaces)

---

## Sobre el Proyecto

**PachiChat** es una aplicacion de chat bot en tiempo real construida en **Unity 6** que demuestra la implementacion practica de los protocolos de red **TCP** y **UDP** desde cero, sin depender de librerias de red de terceros.

La aplicacion simula un flujo real de soporte al cliente:

1. Un **bot automatico** establece contacto inicial con el usuario via **UDP** (conexion ligera y rapida).
2. Tras la respuesta del usuario, el sistema hace una **transicion transparente a TCP** para continuar con un **agente humano** (comunicacion confiable y orientada a conexion).

Ambas partes (cliente y servidor/agente) corren en el mismo ejecutable de Unity, en escenas separadas cargadas de forma aditiva.
---

## Caracteristicas

- **Seleccion de protocolo**: El usuario puede iniciar la sesion en modo **TCP** o **UDP**
- **Mensajeria bidireccional** en tiempo real entre cliente y agente
- **Envio y visualizacion de imagenes** directamente dentro del chat (PNG)
- **Envio y apertura de PDFs** con un toque/clic desde el chat
- **Transicion automatica UDP a TCP** al escalar de bot a agente humano
- **Fragmentacion de archivos via UDP** con ensamblado automatico en el receptor
- **Thread-safe**: todos los callbacks de red se despachan al hilo principal de Unity
- **Confirmacion visual** de mensajes y archivos recibidos en la UI
- **Arquitectura basada en interfaces** (`IServer`, `IClient`, `IChatConnection`) para maxima extensibilidad

---

## Arquitectura

El proyecto sigue una arquitectura en capas con separacion clara entre red, logica y UI:

```
┌─────────────────────────────────────────────────────────┐
│                        UI Layer                         │
│          ChatUIManager          ServerUIManager         │
│               (Cliente)              (Agente)           │
└──────────────────────┬──────────────────────────────────┘
                       │ Events
┌──────────────────────▼──────────────────────────────────┐
│                   ChatManager                           │
│         (Orquestador central de logica y red)           │
│                                                         │
│  · Gestiona flujo bot -> agente                         │
│  · Coordina transicion UDP -> TCP                       │
│  · Despacha callbacks al main thread                    │
└────────────┬──────────────────────────┬─────────────────┘
             │                          │
┌────────────▼────────────┐  ┌──────────▼────────────────┐
│      UDP Layer          │  │       TCP Layer            │
│  UDPServer + UDPClient  │  │  TCPServer + TCPClient     │
│  (Fase bot / inicial)   │  │  (Fase agente / archivos)  │
│  Puerto: 5555           │  │  Puerto: 5556              │
└────────────┬────────────┘  └──────────┬────────────────┘
             │                          │
┌────────────▼────────────┐  ┌──────────▼────────────────┐
│   ImageChunker          │  │   FileTransferData         │
│   ImageAssembler        │  │   (JSON + Base64)          │
│   (Fragmentacion UDP)   │  │   (Framing TCP)            │
└─────────────────────────┘  └────────────────────────────┘
```

### Diagrama de secuencia del flujo principal

```
Cliente (UDP)          Bot/Servidor (UDP)         Agente (TCP)
     │                        │                        │
     │──── CONNECT ──────────>│                        │
     │<─── CONNECTED ─────────│                        │
     │                        │                        │
     │<─── "Hola! Bienvenido" │                        │
     │                        │                        │
     │──── [respuesta] ──────>│                        │
     │<─── "Te conecto con    │                        │
     │      un agente..."     │                        │
     │                        │                        │
     │    [UDP se desconecta] │                        │
     │                        │                        │
     │<════════════════ TCP CONNECT ═══════════════════│
     │                                                 │
     │<════════════════ Chat en tiempo real ═══════════│
     │════════════════> Texto / Imagen / PDF ══════════│
```

---

## Instalacion

### 1. Clonar el repositorio

```bash
git clone https://github.com/Daveyify/-ChatBot---TCP-UDP.git
cd -ChatBot---TCP-UDP
```

### 2. Abrir en Unity

1. Abre **Unity Hub**
2. Haz clic en **"Add project from disk"**
3. Selecciona la carpeta raiz del repositorio clonado
4. Asegurate de usar Unity version **6000.3.8f1**
5. Abre el proyecto

### 3. Verificar la configuracion

- En Unity Editor, abre `Assets/Chatbot/Scenes/Chabot_User.unity`
- Verifica que el `ChatManager` tenga asignados `UDPServer`, `UDPClient` y `TCPClient` en el Inspector
- Verifica que `ServerUIManager` tenga asignado su `TCPServer`

---

## Uso

### Iniciar la aplicacion

1. **Abre la escena principal**: `Assets/Chatbot/Scenes/Chabot_User.unity`
2. Presiona **Play** en el Unity Editor
3. La escena del servidor (`Chatbot_Server`) se carga de forma **aditiva automaticamente**

### Iniciar el chat

1. El bot iniciara la conversacion por **UDP**
2. Escribe tu respuesta y presiona **Enviar**
3. El sistema **escalara automaticamente a TCP** para conectarte con el agente
4. Se conectara a un agente y abrira un chat en **tiempo real**

### Enviar mensajes

| Accion | Como hacerlo |
|---|---|
| Texto | Escribe en el campo de texto y presiona el boton enviar o Enter |
| Imagen | Presiona el boton del clip y selecciona PNG |
| PDF | Presiona el boton del clip y selecciona un archivo PDF |

### Abrir un PDF recibido

Haz **clic o tap** sobre la burbuja del PDF en el chat. Se abrira automaticamente con el visor predeterminado del sistema.

---

## Estructura del Proyecto

```
Assets/
└── Chatbot/
    ├── Assets/                      # Imagenes y recursos de UI
    │   ├── backgroundChat.png
    │   ├── Boton.png
    │   ├── Send.png
    │   └── ...
    ├── Prefabs/
    │   ├── ChatBubble.prefab        # Burbuja de chat reutilizable
    │   └── ProtocolUpdateTxt.prefab # Indicador de cambio de protocolo
    ├── Scenes/
    │   ├── Chabot_User.unity        # Escena del cliente
    │   └── Chatbot_Server.unity     # Escena del agente/servidor
    └── Scripts/
        ├── ChatManager/
        │   ├── ChatManager.cs       # Orquestador principal
        │   ├── ChatBubble.cs        # Componente de burbuja de chat
        │   ├── ChatUIManager.cs     # UI del cliente
        │   └── ServerUIManager.cs   # UI del agente
        ├── Interface/
        │   ├── IChatConnection.cs   # Interfaz base de conexion
        │   ├── IClient.cs           # Interfaz de cliente
        │   └── IServer.cs           # Interfaz de servidor
        ├── TCP/
        │   ├── TCPClient.cs         # Cliente TCP
        │   └── TCPServer.cs         # Servidor TCP
        ├── UDP/
        │   ├── UDPClient.cs         # Cliente UDP
        │   └── UDPServer.cs         # Servidor UDP
        ├── FileTransferData.cs      # Modelo de transferencia de archivos
        ├── ImageChunker.cs          # Fragmentador de archivos para UDP
        ├── ImageAssembler.cs        # Ensamblador de chunks UDP
        └── LoadScene.cs             # Carga aditiva de escenas
```

---

## Flujo de la Aplicacion

### Fase 1 - Conexion UDP (Bot automatico)

```
[Cliente presiona "Comenzar"]
        ↓
UDPServer inicia en puerto 5555
UDPClient envia "CONNECT" al servidor
        ↓
Servidor responde "CONNECTED"
        ↓
Bot envia: "Hola! Bienvenido al chat. En que puedo ayudarte?"
        ↓
Usuario responde -> Bot envia: "Gracias por tu respuesta. Ahora te voy a conectar con un agente."
```

### Fase 2 - Transicion a TCP

```
[ContinueBotFlow() se ejecuta]
        ↓
TCPServer arranca en puerto 5556
Delay de 500ms para estabilizacion
TCPClient se conecta al TCPServer
        ↓
UDP se desconecta (UDPClient + UDPServer)
_isTCPActive = true
        ↓
UI muestra: "Conectado con un agente (TCP)"
```

### Fase 3 - Chat en tiempo real con TCP

```
Cliente <-> Agente: mensajes de texto ilimitados
Cliente <-> Agente: imagenes (PNG) con preview en chat
Cliente <-> Agente: PDFs con apertura al hacer clic
```

### Transferencia de archivos en UDP (chunking)

Para enviar imagenes o PDFs por UDP (donde no hay framing nativo):

```
Archivo -> Base64 -> Division en chunks de 40,000 bytes
                             ↓
    IMG_START:{id}:{total_chunks}
    IMG_CHUNK:{id}:{index}:{base64_data}
    IMG_CHUNK:{id}:{index}:{base64_data}
    ...
    IMG_END:{id}
                             ↓
    Receptor ensambla chunks -> reconstruye archivo -> muestra en UI
```

### Transferencia de archivos en TCP (framing)

TCP usa un protocolo de framing simple de 4 bytes:

```
[ 4 bytes: longitud del payload ][ payload: JSON con Base64 del archivo ]

Ejemplo payload:
##FILE##{"fileName":"foto.png","fileType":"image","base64Data":"iVBOR..."}
```

---

## Diferencias TCP vs UDP

| Caracteristica | TCP | UDP |
|---|---|---|
| **Tipo de conexion** | Orientado a conexion (handshake) | Sin conexion (connectionless) |
| **Confiabilidad** | Garantizada, todos los paquetes llegan | No garantizada, puede haber perdida |
| **Orden de paquetes** | Garantizado | No garantizado |
| **Velocidad** | Ligeramente mas lento (overhead de ACK) | Mas rapido (menor overhead) |
| **Transferencia de archivos** | Framing de 4 bytes + JSON/Base64 | Chunking manual con reensamblado |
| **Soporte en este proyecto** | Texto + Imagen | Texto + PDF |
| **Puerto usado** | 5556 | 5555 |
| **Uso en el flujo** | Fase de agente humano | Fase inicial del bot |
| **Implementacion** | `TcpClient` / `TcpListener` | `UdpClient` |

### Por que UDP primero?

UDP es ideal para la fase del bot porque:
- La latencia es menor, dando sensacion de respuesta inmediata
- El bot envia mensajes cortos donde la perdida ocasional no es critica
- Demuestra la capacidad del sistema de operar en ambos protocolos

### Por que TCP para el agente?

TCP es obligatorio para la fase de agente porque:
- Los archivos deben llegar completos e integros
- El agente humano requiere un canal confiable y ordenado
- El framing TCP simplifica enormemente el manejo de archivos binarios

---

## Interfaces

### Pantalla del Cliente (Chabot_User)

| Elemento | Descripcion |
|---|---|
| Chat scroll | Historial de conversacion con burbujas alineadas por remitente |
| Campo de texto | Input para escribir mensajes |
| Boton Enviar | Envia el mensaje de texto |
| Boton clip | Abre el selector de archivos (imagen o PDF) |
| Indicador de estado | Muestra el protocolo activo y estado de conexion |
| Burbujas azules | Mensajes propios (derecha) |
| Burbujas naranja | Mensajes del bot/agente (izquierda) |
| Burbujas sistema | Cambios de protocolo y eventos de conexion (centro) |

### Pantalla del Agente (Chatbot_Server)

| Elemento | Descripcion |
|---|---|
| Chat scroll | Historial desde perspectiva del agente |
| Campo de texto | Input para responder al cliente |
| Boton Enviar | Envia respuesta al cliente |
| Boton clip | Envia imagen o PDF al cliente |
| Burbujas azules | Mensajes propios del agente (derecha) |
| Burbujas naranja | Mensajes del cliente (izquierda) |

### Indicadores visuales de recepcion

- **Mensaje de texto recibido**: aparece burbuja en el lado izquierdo instantaneamente
- **Imagen recibida**: se renderiza dentro del chat en la burbuja con las proporciones correctas
- **PDF recibido**: burbuja con nombre del archivo y texto "Tap to open"
- **Cambio de protocolo**: burbuja de sistema centrada con fondo translucido

---

## Demo

Video de YouTube: [Ver funcionamiento completo](https://vimeo.com/1172376151)

El video muestra:
1. Flujo completo con **UDP** (bot automatico)
2. Flujo completo con **TCP** (agente humano)
3. Envio y recepcion de **imagen** (PNG)
4. Envio y recepcion de **PDF** con apertura al hacer clic