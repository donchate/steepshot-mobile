﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Android.Media;
using Android.OS;
using Java.Lang;
using Java.Nio;
using Steepshot.CameraGL.Encoder;
using Steepshot.CameraGL.Enums;
using File = Java.IO.File;
using Object = Java.Lang.Object;
using Thread = Java.Lang.Thread;

namespace Steepshot.CameraGL
{
    public class MuxerWrapper : Object, IRunnable
    {
        public Action<string> VideoRecorded;
        private MediaMuxer Muxer { get; set; }
        private string _path;
        private readonly Dictionary<int, (BaseMediaEncoder Encoder, CircularBuffer Buffer)> _encoders;

        private volatile MuxerHandler _handler;
        private readonly object _readyFence = new object();
        private bool _ready;
        private bool _running;

        public MuxerWrapper()
        {
            _encoders = new Dictionary<int, (BaseMediaEncoder Encoder, CircularBuffer Buffer)>();
        }

        public void Reset(string path, MuxerOutputType outputType)
        {
            if (IsMuxing())
            {
                Stop();
            }
            else
            {
                ReleaseMuxer();
            }

            var fs = new FileStream(path, FileMode.CreateNew);
            var file = new File(fs.Name);
            _path = fs.Name;
            Muxer = new MediaMuxer(file.ToString(), outputType);
        }

        private void Start()
        {
            lock (_readyFence)
            {
                if (_running)
                {
                    return;
                }
                _running = true;
                new Thread(this).Start();
                while (!_ready)
                {
                    try
                    {
                        Monitor.Wait(_readyFence);
                    }
                    catch (InterruptedException)
                    {
                        // ignore
                    }
                }
            }

            _handler.SendMessage(_handler.ObtainMessage((int)MuxerMessages.Start));
        }

        private void Stop()
        {
            _handler.SendMessage(_handler.ObtainMessage((int)MuxerMessages.Stop));
        }

        public async void WriteSampleData(int trackIndex, ByteBuffer buffer, MediaCodec.BufferInfo bufferInfo)
        {
            _encoders[trackIndex].Buffer.Add(buffer, bufferInfo);
            await Task.Run(() =>
            _handler.SendMessage(_handler.ObtainMessage((int)MuxerMessages.WriteSampleData, trackIndex, 0, bufferInfo))).ConfigureAwait(false);
        }

        public bool IsMuxing()
        {
            lock (_readyFence)
            {
                return _running;
            }
        }

        public void Run()
        {
            Looper.Prepare();

            lock (_readyFence)
            {
                _handler = new MuxerHandler(this);
                _ready = true;
                Monitor.Pulse(_readyFence);
            }

            Looper.Loop();

            lock (_readyFence)
            {
                _ready = _running = false;
                _handler = null;
            }
        }

        public void HandleStart()
        {
            Muxer.Start();
        }

        public void HandleStop()
        {
            Muxer?.Stop();
            ReleaseMuxer();
            VideoRecorded?.Invoke(_path);
        }

        private void ReleaseMuxer()
        {
            Muxer?.Release();
            Muxer = null;
        }

        public void HandleWriteSampleData(int trackIndex, MediaCodec.BufferInfo bufferInfo)
        {
            var buffer = _encoders[trackIndex].Buffer;
            var data = buffer.GetTailChunk(bufferInfo);
            Muxer.WriteSampleData(trackIndex, data, bufferInfo);
            buffer.RemoveTail();
        }

        public int AddTrack(BaseMediaEncoder encoder, MediaFormat format)
        {
            if (IsMuxing())
                throw new IllegalStateException("Muxer already started");

            if (_encoders.Any(x => x.Value.Encoder.Type == encoder.Type))
                throw new IllegalArgumentException("You haver already registered encoder with the same type.");

            var trackIndex = Muxer.AddTrack(format);

            lock (_encoders)
            {
                _encoders.Add(trackIndex, (encoder, new CircularBuffer(encoder.Format, 20000)));
                if (_encoders.Any(x => x.Value.Encoder.Type == EncoderType.Video) && _encoders.Any(x => x.Value.Encoder.Type == EncoderType.Audio))
                {
                    Start();
                }
            }

            return trackIndex;
        }

        public void StopTrack(BaseMediaEncoder encoder)
        {
            lock (_encoders)
            {
                _encoders.Remove(encoder.TrackIndex);
                if (_encoders.Count == 0)
                    Stop();
            }
        }
    }
}