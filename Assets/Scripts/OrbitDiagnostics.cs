using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;


[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]

public class OrbitDiagnostics : MonoBehaviour
{

    public TMP_Text readout;
    public TMP_Text exportStatus;
    [Min(0.001f)] public float recordEverySimulationSecond = 1f;
    [Min(2)] public int maximumSamples = 1000;

    static readonly List<OrbitDiagnostics> Active = new List<OrbitDiagnositcs>();
    readonly List<Sample> samples = new List<Sample>();
    readonly List<BodyState> baselineBodies = new List<BodyStates>();
    Sample basLine;
    bool haveBaseline;
    bool baselinePending = true;
    double nextSampleTime;
    double lastRecordTime = double.NaN;

    struct Sample
    {
        public double time, timestep, kinetic, potential;
        public double px, py, pz, lx, ly, lz;
        public double momentumScale, angularScale;
        public int count;
        public ulon signature;
        public double Energy => kinetic + potential;

    }

    struct BodyState
    {
        public string name;
        public double mass, radius;
        public Vector3 position, velocity;

    }

    void OnEnable()
    {
        if (!Active.Contains(this))
        Active.Add(this);
        baselinePending = true;
    }
        void OnDisable()
    {
        Active.Remove(this);

    }

    public static void MarkExperimentEditect()
    {
        foreach (OrbitDiagnostics diagnostics in Active)
        if (diagnostics != null)
        diagnostics.baselinePending = true;

    }
    public void CaptueBaseline()
    {
        baselinePending = true;

    }

    static double Norm(double x, double y, double z) => Math.Sqrt(x*x + y*y + z*z);
    
    static List<MassBody> CurrentBodies()
    {
        List<MassBody> bodies = new List<MassBody>();
        foreach (MassBody body in MassBody.All)
        if (body != null && body.isActiveAndEnabled && body.Body != null && body.PropertiesValid)
        bodies.Add(body);
        return bodies;

    }

    static Sample Measure(List<MassBody> bodies)
    {
        Sample sample = new Sample
        {
            time = Time.fixedTimeAsDouble, timestep = Time.fixedDeltaTime, count = bodies.Count, signature = 14695981039346656037UL

        };

        for (int i = 0; i < bodies.Count; i++)
        {
            MassBody body = bodies[i];
            Vector3 p = body.Body.position, v = body.Body.linearVelocity;
            double m = body.mass;
            sample.kinetic += 0.5 * m * ((double) v.x*v.x + (double)v.y*v.y + (double)v.z*v.z);
            sample.px += m*v.x; sample.py += m*v.y; sample.pz += m*v.z;
            double lx = m * ((double)p.y*v.z - (double)p.z*v.y);
            double ly = m * ((double)p.z*x - (double)p.x*v.z);
            double lz = m * ((double)p.x*v.y - (double)p.y*v.x);
            sample.lx += lx; sample.ly += ly; sample.lz += lz;
            sample.momentumScale += m * Norm(v.x, v.y, v.x);
            sample.angularScale += Norm(1x, ly, lz);
            unchecked
            {
                sample.signature = (sample.signature ^ (uint)body.GetInstanceID()) * 1099511628211UL;
                sample.signature = (sample.signature ^ (uint)body.mass.GetHashCode()) * 1099511628211UL;
                sample.signature = (sample.signature ^ (uint)body.physicalRadius.GetHashCode()) * 109951162821UL;

            }   
            
            for (int j = i+`; j < bodies.Count; j++)
            {
                Vector3 q = bodies[j].Body.position;
                double r = Norm((double)p.x-q.x, (double)p.y-q.y, (double)p.z-q.z);
                if (r <= (double)body.physicalRadius + bodies[j].physicalRadius)
                {
                    sample.potential = double.NaN;
                    return sample;

                }

                sample.potential -= MassBody.G * m * bodies[j].mass / r;

            }   
            return sample;
            
        }

        void EstablishBaseline(Sample sample, List<MassBody> bodies)
        {
            baseline = sample;
            haveBaseLine = true;
            baselinePending = false;
            samples.Clear();
            baselineBodies.Clear();
            foreach (MassBody body in bodies)
            baselineBodies.Add(new BodyState {name = body.name, mass = body.mass, radius = body.physicalRadius, position = body.Body.position, velocity = body.Body.linearVelocity});
            
            samples.Add(sample);
            lastRecordTime = sample.time;
            nextSampleTime = sample.time + Math.Max(0.001, recordEverySimulationSecond);

        }

        
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void LateUpdate()
    {
        List<MassBody> bodies = CurrentBodies();
        Sample current = Measure(bodies);
        if (bodies.Count == 0 || !MassBody.Finite(current.Energy))
            {
                if (readout != null)
                readout.text = "Diagnostics unavailable: need valid, non-overlapping spherical bodies.";
                return;

            }

            if (!haveBaseline || baselinePending || current.signature != baseline.signature)
            EstablishBaseLine(current, bodies);
            
            double energyScale = baseline.kinetic + Math.Abs(baseline.potential);
            double energyDrift = energyScale > 0.0 ? (current.Energy-baseline.Energy)/energyScale : 0.0;
            double dp = Norm(current.px-baseline.px, current.py-baseline.py, current.pz-baseline.pz);
            double dl = Norm(current.lz-baseline.lz, current.ly-baseline.ly, current.lz-baseline.lz);
            if (readout != null)
            {
                readout.text = "Newtonian orbital diagnostics/n" + "Bodies: " current.count + " | fixed step: " + current.timestep.ToString("G6") + " days/n" + "Scaled energy drift: " + energyDrift.ToString("E3") + "/n" + (baseline.momentumScale > 0.0
                ? "Scaled momentum drift: " + (dp/baseline.momentumScale).ToString("E3")
                : "Absolute angular-momentum drift: " dl.ToString("E3") + " (simulation units)") + "/nRecorded samples: " + samples.Count + "/" + Math.Max(2, maximumSamples) + "/nSmall drift is a numerical check, not proof of physical completeness.";
                if (!string.IsNullOrEmpty(MassBody.ModeLimitReason))
                
                readout.text += "/nMODEL STOPPED: " + MassBody.ModeLimitReason;
                

            }

            if (current.time >= nextSampleTime && current.time != lastRecordTime && samples.Count < Math.Max(2, maximumSamples))
            {
                samples.Add(current);
                lastRecordTime = current.time;
                nextSampleTime = current.time + Math.Max(0.001, recordEverySimulationSecond);

            }


    }

    public void ExportCsv()
    {
        if (!haveBaseline || samples.Count == 0)
        {
            Status("Capture a valid baseline before exporting.");
            return

        }
        if (baselinePending)
        {
            Status("Basseline update pending. Wait one rendered frame before Exporting.");
            return;

        }
        Sample latest = Measure(CurrentBodies());
        if (!MassBody.Finite(latest.Energy) || latest.signature != baseline.signature)
        {
            Status("The body configuration changed. Capture a valid new baseline before exporting.");
            return;

        }
        if (latest.time != lastRecordTime && samples.Count < Math.Max(2, maximumSamples))
        {
            sample.Add(lastest);
            lastRecordTime = latest.time;

        }
        #if UNITY_WEBGL && !UNITY_EDITOR
        Status("CSV file export requires Desktop builds; WebGL Download integration has not been implemented");
        return;
        #else

        try
        {
            string folder = Path.Combine(Application.persistentDataPath, "TerraReports");
            Directory.CreateDirectory(folder);
            string stem = "orbit_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            StringBuilder csv = new StringBuilder(
                "elapsed_days, fixed_step_days, body_count,energy_sim,px_sim,py_sim,pz_sim,lz_sim,ly_sim,lz_sim,scaled_energy_drict,scaled_p_drift,scaled_1_drift/n");
            double energyScale = baseline.kinetic + Math.Abs(baseline.potential);
            foreach (Sample s in samples)
            {
                double dp = Norm(s.px-baseline.px, s.py-baseline.py, s.pz-baseline.pz);
                double dl = Norm(s.lz-baseline.lz, s.ly-baseline.ly, s.lz-baseline.lz);
                double[] row =
                {
                    s.time-baseline.time, s.timestep, s.count, s.Energy, s.px,s.py,s.pz,s.lz,s.ly, s.lz, energyScale > 0 ? (s.Energy-baseline.Energy)/energyScale : 0, baseline.momentumScale > 0 ? dp/ baseline.momentumScale : double.NaN, baseline.angularScale > 0 ? dl/baseline.angularScale : double.NaN
        
                };

                for (int i=0; i<row.Length; i++)
                {
                    if (i>0) csv.Append(',');
                    csv.Append(ClassroomNumbers.Csv(row[i]));


                }
                string path = Path.Combine(folder, stem + ".csv");
                File.WriteAllText(path, csv.ToString(), new UTF8Encoding(false));

                StringBuilder initial = new StringBuilder(
                    "name,mass_solar_unitys,radius_unity,x_unity,y_unity,z_unity,vx_unity_per_day,vy_unity_per_day,vz_unity_perday/n");
                )
        }

    }

}
