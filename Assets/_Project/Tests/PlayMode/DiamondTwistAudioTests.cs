using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace _Project.Tests.PlayMode
{
    /// <summary>Verifies that puzzle turns use the shared 2D sound pool.</summary>
    public sealed class DiamondTwistAudioTests
    {
        private const int AudioSampleRate = 44100;
        private const int ClipDurationSeconds = 5;

        private static readonly string[] PieceFieldNames =
        {
            "Corner_X_Minus",
            "Corner_X_Plus",
            "Corner_Y_Minus",
            "Corner_Y_Plus",
            "Corner_Z_Minus",
            "Corner_Z_Plus",
            "FaceCenter_NegativeX_NegativeY_NegativeZ",
            "FaceCenter_NegativeX_NegativeY_PositiveZ",
            "FaceCenter_NegativeX_PositiveY_NegativeZ",
            "FaceCenter_NegativeX_PositiveY_PositiveZ",
            "FaceCenter_PositiveX_NegativeY_NegativeZ",
            "FaceCenter_PositiveX_NegativeY_PositiveZ",
            "FaceCenter_PositiveX_PositiveY_NegativeZ",
            "FaceCenter_PositiveX_PositiveY_PositiveZ"
        };

        private static readonly Vector3[] PiecePositions =
        {
            Vector3.left,
            Vector3.right,
            Vector3.down,
            Vector3.up,
            Vector3.back,
            Vector3.forward,
            new(-1f, -1f, -1f),
            new(-1f, -1f, 1f),
            new(-1f, 1f, -1f),
            new(-1f, 1f, 1f),
            new(1f, -1f, -1f),
            new(1f, -1f, 1f),
            new(1f, 1f, -1f),
            new(1f, 1f, 1f)
        };

        private readonly List<Object> _objects = new();

        /// <summary>Destroys the temporary puzzle, test scope, manager and audio assets.</summary>
        [TearDown]
        public void TearDown()
        {
            for (int index = _objects.Count - 1; index >= 0; index--)
            {
                if (_objects[index] != null)
                {
                    Object.DestroyImmediate(_objects[index]);
                }
            }

            _objects.Clear();
        }

        /// <summary>An early-enabled controller waits for real injection, then plays once in 2D.</summary>
        [UnityTest]
        public IEnumerator EarlyEnableWaitsForAudio()
        {
            AudioClip clip = CreateClip();
            Component audioManager = CreateAudioManager();
            GameObject puzzle = CreatePuzzle(clip);
            Component controller = puzzle.GetComponent(
                System.Type.GetType("Play.DiamondTwistController, Assembly-CSharp", true));

            puzzle.SetActive(true);
            yield return null;

            Assert.That(GetPlayingSources(audioManager, clip), Is.Empty);

            CreateLifetimeScope(audioManager, controller);

            AudioSource[] playingSources = GetPlayingSources(audioManager, clip);
            Assert.That(playingSources, Has.Length.EqualTo(1));
            Assert.That(playingSources[0].spatialBlend, Is.Zero);
            Assert.That(puzzle.GetComponent<AudioSource>(), Is.Null);
        }

        /// <summary>Every turn refreshes the silence window, without restricting ordinary effects.</summary>
        [UnityTest]
        public IEnumerator TurnSoundWaitsForSilence()
        {
            AudioClip clip = CreateClip();
            Component manager = CreateAudioManager();
            MethodInfo turn = manager.GetType().GetMethod("PlayTurnSFX");
            MethodInfo stop = manager.GetType().GetMethod("StopAllSFX");
            turn.Invoke(manager, new object[] { clip });
            Assert.That(CountClips(manager, clip), Is.EqualTo(1));
            stop.Invoke(manager, null);

            yield return new WaitForSecondsRealtime(1.1f);
            turn.Invoke(manager, new object[] { clip });
            Assert.That(CountClips(manager, clip), Is.Zero);
            yield return new WaitForSecondsRealtime(1.1f);
            turn.Invoke(manager, new object[] { clip });
            Assert.That(CountClips(manager, clip), Is.Zero);

            yield return new WaitForSecondsRealtime(2.1f);
            turn.Invoke(manager, new object[] { clip });
            Assert.That(CountClips(manager, clip), Is.EqualTo(1));
            turn.Invoke(manager, new object[] { clip });
            Assert.That(CountClips(manager, clip), Is.EqualTo(1));

            manager.GetType().GetMethod("PlaySFX").Invoke(manager, new object[] { clip, 1f, 1f });
            Assert.That(CountClips(manager, clip), Is.EqualTo(2));
        }

        /// <summary>Counts occupied pool sources without depending on audio device playback.</summary>
        private static int CountClips(Component manager, AudioClip clip)
        {
            int count = 0;
            foreach (AudioSource source in manager.GetComponentsInChildren<AudioSource>(true))
            {
                if (source.clip == clip)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>Creates a long silent clip so its pooled source remains observable.</summary>
        /// <returns>The temporary clip used by the turn controller.</returns>
        private AudioClip CreateClip()
        {
            AudioClip clip = AudioClip.Create(
                "DiamondTurnTestClip",
                AudioSampleRate * ClipDurationSeconds,
                1,
                AudioSampleRate,
                false);
            _objects.Add(clip);
            return clip;
        }

        /// <summary>Creates an AudioManager with spare capacity to expose duplicate play requests.</summary>
        /// <returns>The active component that owns the test sound pool.</returns>
        private Component CreateAudioManager()
        {
            System.Type managerType = System.Type.GetType("GameSystem.AudioManager, Assembly-CSharp", true);
            GameObject managerObject = new("DiamondTwistAudioManagerTest");
            _objects.Add(managerObject);
            managerObject.SetActive(false);

            Component manager = managerObject.AddComponent(managerType);
            managerObject.SetActive(true);
            return manager;
        }

        /// <summary>Builds fourteen valid pieces and assigns the serialized turn clip.</summary>
        /// <param name="clip">The test sound assigned to the turn.</param>
        /// <returns>An inactive puzzle ready to start its first turn when enabled.</returns>
        private GameObject CreatePuzzle(AudioClip clip)
        {
            System.Type controllerType = System.Type.GetType("Play.DiamondTwistController, Assembly-CSharp", true);
            GameObject puzzle = new("DiamondTwistAudioTestPuzzle");
            _objects.Add(puzzle);
            puzzle.SetActive(false);

            Component controller = puzzle.AddComponent(controllerType);
            Mesh mesh = new() { name = "DiamondTwistAudioTestMesh" };
            _objects.Add(mesh);

            for (int index = 0; index < PieceFieldNames.Length; index++)
            {
                GameObject piece = new(PieceFieldNames[index]);
                piece.transform.SetParent(puzzle.transform, false);
                piece.transform.localPosition = PiecePositions[index];
                piece.AddComponent<MeshFilter>().sharedMesh = mesh;
                SetField(controller, PieceFieldNames[index], piece.transform);
            }

            SetField(controller, "_turnClip", clip);
            return puzzle;
        }

        /// <summary>Uses VContainer's component registration to inject after the puzzle is enabled.</summary>
        /// <param name="audioManager">The global service instance registered in the test scope.</param>
        /// <param name="controller">The already-enabled puzzle component.</param>
        private void CreateLifetimeScope(Component audioManager, Component controller)
        {
            LifetimeScope scope = LifetimeScope.Create(builder =>
            {
                builder.RegisterInstance(audioManager, audioManager.GetType()).AsSelf();
                builder.RegisterComponent(controller).AsSelf();
            }, "DiamondTwistAudioTestScope");

            _objects.Add(scope.gameObject);
        }

        /// <summary>Finds currently playing sources for the unique test clip.</summary>
        /// <param name="audioManager">The manager whose pooled children are inspected.</param>
        /// <param name="clip">The test clip used to identify matching playback.</param>
        /// <returns>Every active pooled source playing that clip.</returns>
        private static AudioSource[] GetPlayingSources(Component audioManager, AudioClip clip)
        {
            List<AudioSource> playingSources = new();
            AudioSource[] sources = audioManager.GetComponentsInChildren<AudioSource>(true);
            foreach (AudioSource source in sources)
            {
                if (source.clip == clip && source.isPlaying)
                {
                    playingSources.Add(source);
                }
            }

            return playingSources.ToArray();
        }

        /// <summary>Assigns a private field needed to configure an isolated test fixture.</summary>
        /// <param name="target">The object receiving the field value.</param>
        /// <param name="fieldName">The exact field to configure.</param>
        /// <param name="value">The value supplied to the fixture.</param>
        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing test field: {fieldName}");
            field.SetValue(target, value);
        }
    }
}
