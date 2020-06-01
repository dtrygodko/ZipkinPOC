object Hello {
  def max(x: Int, y: Int) = if (x > y) x else y

  def main(args: Array[String]): Unit = {
    var i = 0
    while (i < args.length) {
      println(args(i))
      i += 1
    }
  }
}
