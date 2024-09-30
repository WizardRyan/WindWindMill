using MidiParser;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

public class SceneManager : MonoBehaviour
{

    public AudioClip Bass;
    public AudioClip Chords;
    public AudioClip Melody;
    public AudioClip Noise;

    public Transform cube;

    private AudioSource song;
    private MidiFile midiFile;
    // 0 Chords, 1 Bass, 2 Melody, 3 Noise
    private List<List<MidiNote>> midiNotes = new List<List<MidiNote>>();
    private List<float[]> audioSamples = new List<float[]>();
    private float timeSinceSongStart;
    private List<int> trackIndices = new List<int> { 0, 0, 0, 0 };

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

        StartCoroutine(StartSongWithDelay(3.0f));

        //ReadAllWavData();

        //foreach(var sample in audioSamples[0])
        //{
        //    Debug.Log(sample);
        //}


    }

    void Update()
    {
        if (!song.isPlaying || midiNotes[0].Count - 1 < trackIndices[0]) return;

        var midiNote = midiNotes[0][trackIndices[0]];

        if (timeSinceSongStart >= midiNote.Time)
        {
            cube.position = new Vector3(midiNote.Pitch - 50, midiNote.Velocity / 10.0f, 0);
            trackIndices[0]++;
        }

        timeSinceSongStart += Time.deltaTime;
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
