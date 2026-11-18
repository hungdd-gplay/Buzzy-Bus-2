// WARNING: Do not modify! Generated file.

namespace UnityEngine.Purchasing.Security {
    public class GooglePlayTangle
    {
        private static byte[] data = System.Convert.FromBase64String("KO90VH5Ei8Rgm2BvYkaJpf3mp7DJden2FEJU8B63UiqtpGI99ALbWe+9rppDlwCj1Qbr60+V4LvIHEMFvzwyPQ2/PDc/vzw8PZoxZAc7F0gEHi1LltkuwNjX4LNIeEOe1qrpGDOtaJ9HBHk7tMNSMrt1EK5VppLRNxXt5gyKsxZr5hGSVQ2CM4lpMr4NvzwfDTA7NBe7dbvKMDw8PDg9PnnMvqboYiPcnMvFqsHKErpZpN/Tj1NdTeCTp/YvR2HW7qVWHALA9937oJUveVHkh6fe7ZeR4V9zdsFWYLOZwPs0+tyXd7rzu90OkFvk2nNWz6/D9xHs6xcBDczEZcqj2mBpdnMHi+VcY0urLX5HkLxeOvOfD++w2XSuAbiHI/t6PD8+PD08");
        private static int[] order = new int[] { 9,11,5,11,8,6,11,9,10,9,11,13,13,13,14 };
        private static int key = 61;

        public static readonly bool IsPopulated = true;

        public static byte[] Data() {
        	if (IsPopulated == false)
        		return null;
            return Obfuscator.DeObfuscate(data, order, key);
        }
    }
}
