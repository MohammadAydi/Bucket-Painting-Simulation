namespace onlyone
{ 
    public readonly struct RopeState
    {
        public readonly double Theta;       
        public readonly double Phi;        
        public readonly double ThetaDot;     
        public readonly double PhiDot;        
        public readonly double EffLength;   
        public readonly double Tension;   
        public readonly double KineticEnergy;
        public readonly double PotentialEnergy;
        public readonly double ElasticEnergy;    
        public readonly double TotalEnergy;

        public RopeState(double theta, double phi, double thetaDot, double phiDot,
            double effLen, double tension,
            double ke, double pe, double elastic)
        {
            Theta = theta; Phi = phi; ThetaDot = thetaDot; PhiDot = phiDot;
            EffLength = effLen; Tension = tension;
            KineticEnergy = ke; PotentialEnergy = pe; ElasticEnergy = elastic;
            TotalEnergy = ke + pe + elastic;
        }
    }
}