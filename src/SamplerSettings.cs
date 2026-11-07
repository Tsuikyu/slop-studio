using System;
using System.IO;

namespace SlopStudio
{
    public sealed class SamplerSettings
    {
        public Guid TrackId { get; set; }
        public string SamplePath { get; set; }
        public int RootNote { get; set; }
        public double Attack { get; set; }
        public double Decay { get; set; }
        public double Sustain { get; set; }
        public double Release { get; set; }
        public SamplerSettings() { RootNote = 60; Attack = .004; Decay = .1; Sustain = 1; Release = .065; }
        public SamplerSettings Copy() { return (SamplerSettings)MemberwiseClone(); }
        public void Validate()
        {
            if (TrackId == Guid.Empty || RootNote < 0 || RootNote > 127 ||
                (SamplePath != null && SamplePath.Length > 2048) ||
                Double.IsNaN(Attack) || Double.IsInfinity(Attack) || Attack < 0 || Attack > 30 ||
                Double.IsNaN(Decay) || Double.IsInfinity(Decay) || Decay < 0 || Decay > 30 ||
                Double.IsNaN(Sustain) || Double.IsInfinity(Sustain) || Sustain < 0 || Sustain > 1 ||
                Double.IsNaN(Release) || Double.IsInfinity(Release) || Release < 0 || Release > 30)
                throw new InvalidDataException("Invalid Sampler settings.");
        }
    }
}
