using MidiParser;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using UnityEngine.Rendering;
using Unity.VisualScripting;

public class SceneManager : MonoBehaviour
{

    public AudioClip Bass;
    public AudioClip Chords;
    public AudioClip Melody;
    public AudioClip Noise;

    public Transform cube;

    public Transform topOfWorld;
    public Transform bottomOfWorld;
    public Transform worldTransform;

    public Transform Sun;

    public Transform WindmillA; 
    public Transform WindmillB;

    public GameObject Melody2Burst;
    public Transform ParticleCenter;

    public Transform bird;

    public float PitchDeltaTolerance = 1.5f;

    private AudioSource song;
    private MidiFile midiFile;
    // 0 Chords, 1 Bass, 2 Melody, 3 Noise
    private List<List<MidiNote>> midiNotes = new List<List<MidiNote>>();
    private List<float[]> audioSamples = new List<float[]>();
    private float timeSinceSongStart;
    private List<int> trackIndices = new List<int> { 0, 0, 0, 0 };
    private int lowestPitch = 99;
    private int highestPitch = 0;

    private List<GlowObject> glowObjects = new List<GlowObject>();
    private List<GlowObject> dimmingObjects = new List<GlowObject>();

    class MidiNote
    {
        public MidiNote(float time, int pitch, int velocity, int timeDelay)
        {
            // Convert to milliseconds
            Time = ((float)time / 1000.0f);
            // Convert to real-time
            Time *= (18.0f / 12.96f);
            Time += ((float)timeDelay / 1000.0f);
            Pitch = pitch;
            Velocity = (float)velocity;
        }
        public float Time;
        public int Pitch;
        public float Velocity;

        public override string ToString()
        {
            return $"Time: {Time} Pitch: {Pitch} Velocity: {Velocity}";
        }
    }

    class GlowObject
    {
        public GlowObject(Material material, SubMeshDescriptor submesh, Vector3 worldSpaceCenter)
        {
            Mat = material;
            SubMesh = submesh;
            WorldSpaceCenter = worldSpaceCenter;
        }

        public void MakeGlow(float intensity)
        {
            maxIntensity = intensity;
            CurrentIntensity = maxIntensity;
            CurrentExposureWeight = 0f;
            Mat.mainTexture = Mat.GetTexture("_EmissiveColorMap");
        }

        public void Update(float deltaTime)
        {
            Mat.SetFloat("_EmissiveExposureWeight", CurrentExposureWeight);
            Mat.SetColor("_EmissiveColor", emissionColor * CurrentIntensity);
            CurrentIntensity -= (deltaTime * 100);
            CurrentExposureWeight += ((deltaTime * 100f) / (maxIntensity));

            CurrentExposureWeight = Mathf.Clamp01(CurrentExposureWeight);
            CurrentIntensity = Mathf.Clamp(CurrentIntensity, 1.0f, maxIntensity);
        }

        public Material Mat;
        public SubMeshDescriptor SubMesh;
        public Transform Tform;
        public Vector3 WorldSpaceCenter;
        public float CurrentIntensity = 1f;
        public float CurrentExposureWeight = 1f;

        private float maxIntensity = 0;

        private Color emissionColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);
    }

    void Start()
    {
        song = GetComponent<AudioSource>();

        ReadMidiData();

        int i = 0;
        foreach (var notes in midiNotes)
        {
            Debug.Log($"-------Notes for track {i}-------");
            foreach (var note in notes)
            {
                Debug.Log(note);
            }
            Debug.Log("\n");
            i++;
        }

        InitGlowObjs();

        StartCoroutine(StartSongWithDelay(3.0f));

        //ReadAllWavData();

        //foreach(var sample in audioSamples[0])
        //{
        //    Debug.Log(sample);
        //}
    }

    void Update()
    {
        foreach(var g in glowObjects)
        {
            g.Update(Time.deltaTime);
        }

        Sun.eulerAngles += new Vector3(Time.deltaTime / 5.0f, 0, 0);

        WindmillA.eulerAngles -= new Vector3(0, 0, Time.deltaTime * 4);
        WindmillB.eulerAngles -= new Vector3(0, 0, Time.deltaTime * 4);

        if (!song.isPlaying || midiNotes[0].Count - 1 < trackIndices[0]) return;

        var midiNote = midiNotes[0][trackIndices[0]];

        if (timeSinceSongStart >= midiNote.Time)
        {
            cube.position = new Vector3(midiNote.Pitch - 50, midiNote.Velocity / 10.0f, 0);

            foreach(var g in glowObjects)
            {
                var pitchDelta = Mathf.Abs(midiNote.Pitch - ConvertToNoteSpace(g.WorldSpaceCenter.y));
                if (pitchDelta <= PitchDeltaTolerance)
                {
                    g.MakeGlow(30f);
                }
                if(midiNote.Pitch == lowestPitch)
                {
                    g.MakeGlow(30f);
                }
            }
            
            trackIndices[0]++;
        }

        var melody2Note = midiNotes[2][trackIndices[2]];

        if (timeSinceSongStart >= melody2Note.Time)
        {
            var pos = new Vector3(midiNote.Pitch - 100, midiNote.Velocity / 10.0f, 0) + ParticleCenter.position;
            Instantiate(Melody2Burst, pos, Quaternion.identity);

            trackIndices[2]++;
        }


        timeSinceSongStart += Time.deltaTime;
    }

    private float ConvertToNoteSpace(float y)
    {
        return Mathf.Lerp(lowestPitch, highestPitch, (y - bottomOfWorld.position.y) / (topOfWorld.position.y - bottomOfWorld.position.y));
    }

    private void InitGlowObjs()
    {
        foreach (Transform t in worldTransform)
        {
            var renderer = t.GetComponent<Renderer>();
            var emissionColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);
            var intensity = 30f;
            Mesh mesh = t.GetComponent<MeshFilter>().mesh;
            Debug.Log("Submeshes: " + mesh.subMeshCount);

            int i = 0;
            //each entery in the array corresponds to a submesh
            foreach (Material m in renderer.materials)
            {
                GlowObject glowie;
                SubMeshDescriptor submesh = mesh.GetSubMesh(i);

                // For whatever reason, the main level submesh reports world coordinates on bound center property already
                if (mesh.subMeshCount == 22)
                {
                    glowie = new GlowObject(m, submesh, (submesh.bounds.max + submesh.bounds.center) / 2f);
                }
                else
                {
                    glowie = new GlowObject(m, submesh, t.TransformPoint(submesh.bounds.center));
                }

                //var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                //cube.transform.position = glowie.WorldSpaceCenter;

                //if (glowie.WorldSpaceCenter.y < topOfWorld.position.y - ((topOfWorld.position.y - bottomOfWorld.position.y) / 2))
                //{
                //    glowie.MakeGlow(intensity);
                //}

                var tex = m.mainTexture;
                m.mainTexture = null;
                glowObjects.Add(glowie);
                //m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                //m.SetInt("_UseEmissiveIntensity", 1);
                //m.SetFloat("_EmissiveIntensity", 20f);
                //m.SetFloat("_EmissiveExposureWeight", 0f);
                //m.EnableKeyword("_EMISSION");
                //m.SetTexture("_EmissiveColorMap", tex);
                //m.SetTexture("_EmissiveColorMap", null);

                //m.SetColor("_EmissiveColor", emissionColor * intensity);
                //RendererExtensions.UpdateGIMaterials(renderer);
                //DynamicGI.SetEmissive(renderer, emissionColor * intensity);
                //DynamicGI.UpdateEnvironment();
                i++;
            }
        }
    }

    private void DoTrackEffect()
    {

    }

    private void ReadAllWavData()
    {
        audioSamples.Add(ReadWavData(Chords));
        audioSamples.Add(ReadWavData(Bass));
        audioSamples.Add(ReadWavData(Melody));
        audioSamples.Add(ReadWavData(Noise));
    }

    private float[] ReadWavData(AudioClip c)
    {
        float[] samples = new float[c.samples * c.channels];
        c.GetData(samples, 0);
        return samples;
    }

    private void ReadMidiData()
    {
        var midiFile = new MidiFile($"{Application.dataPath}\\Audio\\windwindmill.mid");

        foreach (var track in midiFile.Tracks)
        {
            if(track.Index > 0)
            {
                midiNotes.Add(new List<MidiNote>());
            }
            foreach (var midiEvent in track.MidiEvents)
            {
                if (midiEvent.MidiEventType == MidiEventType.NoteOn)
                {
                    midiNotes[track.Index - 1].Add(new MidiNote(midiEvent.Time, midiEvent.Note, midiEvent.Velocity, track.Index == 1 ? 78 : 0));

                    if(track.Index == 1)
                    {
                        if(lowestPitch > midiEvent.Note)
                        {
                            lowestPitch = midiEvent.Note;
                        }
                        if (highestPitch < midiEvent.Note)
                        {
                            highestPitch = midiEvent.Note;
                        }
                    }
                }
            }
        }
    }

    private IEnumerator StartSongWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        song.Play();
    }
}
