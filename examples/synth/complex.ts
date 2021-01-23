// Complex number
// https://mathworld.wolfram.com/ComplexNumber.html
export class Complex {
    re: f32;
    im: f32;

    constructor(re: f32, im: f32) {
        this.re = re;
        this.im = im;
    }

    abs(): f32 {
        return Mathf.sqrt(this.re * this.re + this.im * this.im);
    }

    conjugate(): Complex {
        return new Complex(this.re, -this.im);
    }

    // Multiplication with a real number
    multiply(a: f32): Complex {
        return new Complex(this.re * a, this.im * a);
    }

    // exp(a+jb) = exp(a) exp(jb) = exp(a)(cos(b) + jsin(b))
    // See: https://mathworld.wolfram.com/EulerFormula.html
    exp(): Complex {
        return new Complex(this.re * Mathf.cos(this.im), this.re * Mathf.sin(this.im));
    }

    // Using operator overloads
    // See: https://www.assemblyscript.org/peculiarities.html#operator-overloads

    @operator("+")
    static __opPlus(a: Complex, b: Complex): Complex {
        return new Complex(a.re + b.re, a.im + b.im);
    }

    @operator("-")
    static __opMinus(a: Complex, b: Complex): Complex {
        return new Complex(a.re - b.re, a.im - b.im);
    }

    @operator("*")
    static __opMultiply(a: Complex, b: Complex): Complex {
        return new Complex(a.re * b.re - a.im * b.im, a.re * b.im + a.im * b.re);
    }

    @operator("/")
    static __opDivision(a: Complex, b: Complex): Complex {
        const denom = b.re * b.re + b.im * b.im;
        return new Complex((a.re * b.re + a.im * b.im) / denom, (a.im * b.re - a.re * b.im) / denom);
    }
}