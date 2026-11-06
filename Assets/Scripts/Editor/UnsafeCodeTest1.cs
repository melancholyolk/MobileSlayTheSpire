using System;

public class UnsafeFixed
{
    public unsafe int SumArray(int[] arr)
    {
        int sum = 0;
        fixed (int* p = arr)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                sum += p[i];
            }
        }
        return sum;
    }
}
