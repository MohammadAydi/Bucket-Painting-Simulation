namespace onlyone
{ 
    public readonly struct RopeState
    {
        public readonly double theta;       
        public readonly double phi;        
        public readonly double thetaDot;     
        public readonly double phiDot;        
        public readonly double effLength;   
        public readonly double tension;   
        public readonly double kineticEnergy;
        public readonly double potentialEnergy;
        public readonly double elasticEnergy;    
        public readonly double totalEnergy;

        public RopeState(double theta, double phi, double thetaDot, double phiDot,
            double effLen, double tension,
            double ke, double pe, double elastic)
        {
            this.theta = theta; this.phi = phi; this.thetaDot = thetaDot; this.phiDot = phiDot;
            effLength = effLen; this.tension = tension;
            kineticEnergy = ke; potentialEnergy = pe; elasticEnergy = elastic;
            totalEnergy = ke + pe + elastic;
        }
    }
}