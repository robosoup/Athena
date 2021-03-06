// Copyright (c) 2016 robosoup
// www.robosoup.com

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Athena
{
    internal class Model : Dictionary<string, Model.Item>
    {
        // Start hyperparameters.
        public const int Dims = 128;
        public const int MaxSize = (int)1e6;
        public const int TrainMin = 10;
        public const int QueryMin = 30;
        // End hyperparameters.

        public Model(bool learnVocab)
        {
            if (learnVocab) LearnVocab();
            else LoadModel();
        }

        public KeyValuePair<string, double>[] Nearest(string phrase, int count)
        {
            var vec = Vector(phrase);
            var bestd = new double[count];
            var bestw = new string[count];
            for (var n = 0; n < count; n++) bestd[n] = -1;

            foreach (var key in Keys)
            {
                var tmp = this[key];
                var sim = Similarity(vec, tmp.Vector);
                for (var c = 0; c < count; c++)
                    if (sim > bestd[c])
                    {
                        for (var i = count - 1; i > c; i--)
                        {
                            bestd[i] = bestd[i - 1];
                            bestw[i] = bestw[i - 1];
                        }

                        bestd[c] = sim;
                        bestw[c] = key;
                        break;
                    }
            }

            var result = new KeyValuePair<string, double>[count];
            for (var i = 0; i < count; i++) result[i] = new KeyValuePair<string, double>(bestw[i], bestd[i]);

            return result;
        }

        public string Nearest(string phrase)
        {
            var results = Nearest(phrase, 1);
            return results.First().Key;
        }

        public void Reduce(int threshold)
        {
            var keys = Keys.ToList();
            foreach (var key in from key in keys let item = this[key] where item.Count < threshold select key)
                Remove(key);
        }

        public void Save()
        {
            Console.WriteLine("Saving model [{0:H:mm:ss}]", DateTime.Now);
            Console.WriteLine();
            var back = string.Format("{0}_{1:yyyyMMddHHmm}.bin", Program.Path_Model.Remove(Program.Path_Model.Length - 4), DateTime.Now);
            if (File.Exists(Program.Path_Model)) File.Move(Program.Path_Model, back);
            using (var bw = new BinaryWriter(File.Open(Program.Path_Model, FileMode.Create)))
            {
                bw.Write(Count);
                bw.Write(Dims);
                foreach (var item in this)
                {
                    bw.Write(item.Key);
                    item.Value.Save(bw);
                }
            }
        }

        private void LearnVocab()
        {
            Console.WriteLine("Learning vocabulary [{0:H:mm:ss}]", DateTime.Now);
            Console.WriteLine();
            double length = new FileInfo(Program.Path_Corpus_1).Length;
            using (var sr = new StreamReader(Program.Path_Corpus_1))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    foreach (var word in line.Split(null as string[], StringSplitOptions.RemoveEmptyEntries))
                        if (!ContainsKey(word)) Add(word, new Item { Count = 1 });
                        else this[word].Count++;

                    if (Count > MaxSize) Reduce(TrainMin);
                    Console.Write("Progress: {0:0.000%}  \r", sr.BaseStream.Position / length);
                }
            }
            Reduce(TrainMin);

            Console.WriteLine("\r\n");
            Console.WriteLine("Vocab size: {0}k", Count / 1000);
            Console.WriteLine();
        }

        private void LoadModel()
        {
            if (!File.Exists(Program.Path_Model))
            {
                Console.WriteLine("Model file not found!");
                Console.WriteLine();
                return;
            }
            Console.WriteLine("Loading model [{0:H:mm:ss}]", DateTime.Now);
            Console.WriteLine();
            using (var br = new BinaryReader(File.Open(Program.Path_Model, FileMode.Open)))
            {
                var words = br.ReadInt32();
                var dims = br.ReadInt32();
                if (dims != Dims)
                {
                    Console.WriteLine("Dimensions don't match!");
                    Console.WriteLine();
                    return;
                }

                for (var w = 0; w < words; w++)
                {
                    var key = br.ReadString();
                    if (!ContainsKey(key)) Add(key, new Item());
                    this[key].Load(br);
                }
            }
        }

        private static float Similarity(float[] vec1, float[] vec2)
        {
            float sim = 0;
            float len1 = 0;
            float len2 = 0;
            var dims = vec1.Length;
            for (var i = 0; i < dims; i++)
            {
                sim += vec1[i] * vec2[i];
                len1 += vec1[i] * vec1[i];
                len2 += vec2[i] * vec2[i];
            }

            if ((len1 == 0) || (len2 == 0)) return 0;

            return sim / (float)(Math.Sqrt(len1) * Math.Sqrt(len2));
        }

        private float[] Vector(string phrase)
        {
            var vec = new float[Dims];
            var keys = phrase.Split(null as string[], StringSplitOptions.RemoveEmptyEntries);
            foreach (var k in keys)
            {
                var sgn = 1;
                var key = k;
                if (key.EndsWith(":"))
                {
                    sgn = -1;
                    key = key.TrimEnd(':');
                }

                if (!ContainsKey(key)) continue;

                var tmp = this[key].Normal;
                for (var i = 0; i < Dims; i++)
                    vec[i] += tmp[i] * sgn;
            }

            return vec;
        }

        public class Item
        {
            public float[] Vector = new float[Dims];
            public int Count;
            public int ID;

            public float[] Normal
            {
                get
                {
                    float len = 0;
                    for (var i = 0; i < Dims; i++) len += Vector[i] * Vector[i];
                    len = (float)Math.Sqrt(len);
                    var normal = new float[Dims];
                    for (var i = 0; i < Dims; i++) normal[i] = Vector[i] / len;
                    return normal;
                }
            }

            public void Load(BinaryReader br)
            {
                Count = br.ReadInt32();
                for (var i = 0; i < Dims; i++)
                    Vector[i] = br.ReadSingle();
            }

            public void Save(BinaryWriter bw)
            {
                bw.Write(Count);
                for (var i = 0; i < Dims; i++)
                    bw.Write(Vector[i]);
            }
        }
    }
}