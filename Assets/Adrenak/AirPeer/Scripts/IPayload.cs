namespace Adrenak.AirPeer {
    public interface IPayload {
        byte[] GetBytes();
        void SetBytes(byte[] bytes);
    }
}
