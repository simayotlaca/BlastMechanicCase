public struct GridPos
{
    public int row;
    public int col;
    public bool isValid;

    public GridPos(int row, int col, bool isValid)
    {
        this.row = row;
        this.col = col;
        this.isValid = isValid;
    }
}