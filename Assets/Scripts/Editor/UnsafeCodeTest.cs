namespace DefaultNamespace
{
    public unsafe class UnsafeCodeTest
    {
        public unsafe int TestPointer()
        {
            int x = 10;
            int* p = &x;
            *p = 20;
            return x;   // 返回 20
        }
    }
}
