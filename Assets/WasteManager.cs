using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using TMPro;
using System.Collections;

public class WasteManager : MonoBehaviour
{
    [Header("Scene References")]
    public GameObject plasticBin;
    public GameObject paperBin;
    public GameObject metalBin;
    public GameObject wasteObject;
    public TextMeshProUGUI detectionText;

    [Header("Audio Clips")]
    public AudioSource plasticAudio;
    public AudioSource paperAudio;
    public AudioSource metalAudio;

    [Header("Lights & Effects")]
    public Light plasticLight;
    public Light paperLight;
    public Light metalLight;
    public ParticleSystem plasticEffect;
    public ParticleSystem paperEffect;
    public ParticleSystem metalEffect;

    // --- Internal vars ---
    private Thread receiveThread;
    private UdpClient client;
    private volatile bool running = true;
    private string receivedData = "";

    private int plasticCount, paperCount, metalCount;
    private float energySaved = 0f;
    private float co2Reduced = 0f;

    void Start()
    {
        // Start UDP listener on same port as Python
        client = new UdpClient(5052);
        receiveThread = new Thread(ReceiveData);
        receiveThread.IsBackground = true;
        receiveThread.Start();

        detectionText.text = "Waiting for detection...";
        DisableAllLights();
    }

    void Update()
    {
        if (!string.IsNullOrEmpty(receivedData))
        {
            string type = receivedData.Trim();
            if (type != "None" && type != "")
            {
                HandleDetection(type);
            }
            receivedData = ""; // reset after handling
        }
    }

    // --- UDP Listener Thread ---
    void ReceiveData()
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
        while (running)
        {
            try
            {
                if (client.Available > 0)
                {
                    byte[] data = client.Receive(ref remoteEP);
                    receivedData = Encoding.UTF8.GetString(data);
                }
                else
                {
                    Thread.Sleep(10);
                }
            }
            catch (SocketException) { }
            catch (System.ObjectDisposedException) { break; }
            catch (System.Exception e) { Debug.Log("UDP Error: " + e.Message); }
        }
    }

    // --- Core Logic ---
    void HandleDetection(string type)
    {
        // 1. Play voice feedback
        switch (type)
        {
            case "Plastic":
                plasticAudio?.Play();
                break;
            case "Paper":
                paperAudio?.Play();
                break;
            case "Metal":
                metalAudio?.Play();
                break;
        }

        // 2. Update counters and eco stats
        if (type == "Plastic") plasticCount++;
        else if (type == "Paper") paperCount++;
        else if (type == "Metal") metalCount++;

        energySaved += 0.05f; // arbitrary metric
        co2Reduced += 0.03f;

        // 3. Update text dashboard
        detectionText.text =
            $"Detected: {type}\n" +
            $"Plastic: {plasticCount}   Paper: {paperCount}   Metal: {metalCount}\n" +
            $"Energy Saved: {energySaved:F2} kWh   CO? Reduced: {co2Reduced:F2} kg";

        // 4. Move object + visual feedback
        MoveToBin(type);
    }

    void MoveToBin(string type)
    {
        GameObject targetBin = null;
        ParticleSystem fx = null;
        Light glow = null;

        switch (type)
        {
            case "Plastic":
                targetBin = plasticBin;
                fx = plasticEffect;
                glow = plasticLight;
                break;
            case "Paper":
                targetBin = paperBin;
                fx = paperEffect;
                glow = paperLight;
                break;
            case "Metal":
                targetBin = metalBin;
                fx = metalEffect;
                glow = metalLight;
                break;
            default:
                return;
        }

        // Turn on light + particle
        DisableAllLights();
        if (glow != null) glow.enabled = true;
        fx?.Play();

        StopAllCoroutines();
        StartCoroutine(MoveWasteToBin(targetBin));
    }

    IEnumerator MoveWasteToBin(GameObject bin)
    {
        Vector3 start = wasteObject.transform.position;
        Vector3 end = bin.transform.position + Vector3.up * 1f;
        float t = 0;

        while (t < 1)
        {
            t += Time.deltaTime;
            wasteObject.transform.position = Vector3.Lerp(start, end, t);
            yield return null;
        }

        yield return new WaitForSeconds(0.5f);
        wasteObject.transform.position = new Vector3(0, 0.5f, -3);
        DisableAllLights();
    }

    void DisableAllLights()
    {
        if (plasticLight != null) plasticLight.enabled = false;
        if (paperLight != null) paperLight.enabled = false;
        if (metalLight != null) metalLight.enabled = false;
    }

    // --- Cleanup ---
    void OnApplicationQuit()
    {
        running = false;
        client?.Close();
        if (receiveThread != null && receiveThread.IsAlive)
            receiveThread.Join(100);
    }

    void OnDestroy()
    {
        running = false;
        client?.Close();
        if (receiveThread != null && receiveThread.IsAlive)
            receiveThread.Join(100);
    }
}
