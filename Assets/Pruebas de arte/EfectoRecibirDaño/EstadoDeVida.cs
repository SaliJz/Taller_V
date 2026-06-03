using UnityEngine;

public class EstadoDeVida : MonoBehaviour
{
    public Material estadoDeVidaMaterial;
    private const string PROP_CONDICION_DAÑO = "_Condicion";
    private const string PROP_TRANSPARENCIA = "_Transparencia";
    private const string PROP_INTENSIDAD_POSTERIZADO = "_IntensidadPosterizado";
    private const string PROP_CONDICION_VIDA = "_Condici_n";
    private const string PROP_TIEMPO_RESPIRACION = "_TiempoRespiraci_n";
    private const string PROP_TAMAÑO_DITHER = "_Tama_oDither";
    private const string PROP_LIMITE_DITHER = "_LimiteDither";
    private const string PROP_CONDICION_PIXEL = "_Condici_nPixel";
    private const string PROP_SIZE_PIXEL = "_SizePixel";

    [Header("Recibir Daño")]
    [Tooltip("Activa efecto de primera herida (Capa1). Se auto-desactiva al terminar.")]
    public bool PrimeraHerida;
    [Tooltip("Activa efecto de heridas posteriores (Capa2). Se auto-desactiva al terminar.")]
    public bool DemasHeridas;  // pal de progra cuando el jugador reciba mucho daño activar este bool, si recibe solo un golpe activar el anterior osea PrimeraHerida
    [Tooltip("Segundos que tardan Transparencia e IntensidadPosterizado en bajar a 0.")]
    [Min(0.01f)]
    public float TiempoHerida = 2f;
    [Tooltip("Valor maximo de Transparencia en el shader.")]
    [Range(0f, 100f)]
    public float Transparencia = 72f;
    [Tooltip("Valor maximo de IntensidadPosterizado en el shader.")]
    [Range(0f, 100f)]
    public float Posterizado = 0f;
    private bool _prevPrimeraHerida;
    private bool _prevDemasHeridas;
    private bool _dañoProcesando;
    private float _dañoElapsed;

    [Header("Vida Baja")]
    [Tooltip("Activa o desactiva el efecto de vida baja.")]
    public bool VidaBajaActiva;  // el de progra puede llamar LowHealthVignette(bool) o cambiar este bool directamente
    [Tooltip("Sincronizado con TiempoRespiracion del shader.")]
    [Range(1f, 6f)]
    public float TiempoRespiracion = 4f;
    [Tooltip("Sincronizado con TamanoDither del shader.")]
    [Range(0f, 100f)]
    public float TamañoDither = 24.5f;
    [Tooltip("Sincronizado con LimiteDither del shader.")]
    [Range(0f, 0.4f)]
    public float LimiteDither = 0.3f;
    private bool _prevVidaBajaActiva;

    [Header("Pixeleado")]
    [Tooltip("Si esta activo el juego esta pixeleado, si no, no.")]
    public bool PixeladoActivo;  // el de progra activa esto en el cambio de etapa
    [Tooltip("Sincronizado con SizePixel del shader.")]
    [Min(1f)]
    public float SizePixel = 150f;
    private bool _prevPixeladoActivo;


    private void Start()
    {
        if (estadoDeVidaMaterial == null)
        {
            Debug.LogError("[EstadoDeVida] No hay Material asignado en estadoDeVidaMaterial.", this);
            enabled = false;
            return;
        }
        SetFloat(PROP_CONDICION_DAÑO, 0f);
        SetFloat(PROP_TRANSPARENCIA, Transparencia);
        SetFloat(PROP_INTENSIDAD_POSTERIZADO, Posterizado);
        SetFloat(PROP_CONDICION_VIDA, 0f);
        SetFloat(PROP_TIEMPO_RESPIRACION, TiempoRespiracion);
        SetFloat(PROP_TAMAÑO_DITHER, TamañoDither);
        SetFloat(PROP_LIMITE_DITHER, LimiteDither);
        SetFloat(PROP_CONDICION_PIXEL, 0f);
        SetFloat(PROP_SIZE_PIXEL, SizePixel);

        _prevPrimeraHerida = false;
        _prevDemasHeridas = false;
        _prevVidaBajaActiva = false;
        _prevPixeladoActivo = false;
        PixeladoActivo = false;
    }

    private void Update()
    {
        HandleDaño();
        HandleVidaBaja();
        HandlePixelado();
        SetFloat(PROP_TIEMPO_RESPIRACION, TiempoRespiracion);
        SetFloat(PROP_TAMAÑO_DITHER, TamañoDither);
        SetFloat(PROP_LIMITE_DITHER, LimiteDither);
        SetFloat(PROP_SIZE_PIXEL, SizePixel);
    }

    private void HandleDaño()
    {
        bool nuevaPrimera = PrimeraHerida && !_prevPrimeraHerida;
        bool nuevaDemas = DemasHeridas && !_prevDemasHeridas;

        if (nuevaPrimera || nuevaDemas)
        {
            int condicion = (nuevaDemas || DemasHeridas) ? 2 : 1;
            ActivarDaño(condicion);
        }

        if (_dañoProcesando)
        {
            _dañoElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_dañoElapsed / TiempoHerida);

            SetFloat(PROP_TRANSPARENCIA, Mathf.Lerp(Transparencia, 0f, t));
            SetFloat(PROP_INTENSIDAD_POSTERIZADO, Mathf.Lerp(Posterizado, 0f, t));

            if (t >= 1f)
                DesactivarDaño();
        }

        _prevPrimeraHerida = PrimeraHerida;
        _prevDemasHeridas = DemasHeridas;
    }

    private void ActivarDaño(int condicion)
    {
        PrimeraHerida = (condicion == 1);
        DemasHeridas = (condicion == 2);

        _dañoElapsed = 0f;
        _dañoProcesando = true;

        SetFloat(PROP_CONDICION_DAÑO, condicion);
        SetFloat(PROP_TRANSPARENCIA, Transparencia);
        SetFloat(PROP_INTENSIDAD_POSTERIZADO, Posterizado);

        PrimeraHerida = false;
    }

    private void DesactivarDaño()
    {
        _dañoProcesando = false;
        PrimeraHerida = false;
        DemasHeridas = false;
        _prevPrimeraHerida = false;
        _prevDemasHeridas = false;

        SetFloat(PROP_CONDICION_DAÑO, 0f);
        SetFloat(PROP_TRANSPARENCIA, Transparencia);
        SetFloat(PROP_INTENSIDAD_POSTERIZADO, Posterizado);
    }

    private void HandleVidaBaja()
    {
        if (VidaBajaActiva == _prevVidaBajaActiva) return;

        SetFloat(PROP_CONDICION_VIDA, VidaBajaActiva ? 1f : 0f);
        _prevVidaBajaActiva = VidaBajaActiva;
    }

    public void LowHealthVignette(bool active)
    {
        // el de progra que llame a este metodo para la vida baja
        VidaBajaActiva = active;
    }

    private void HandlePixelado()
    {
        if (PixeladoActivo == _prevPixeladoActivo) return;

        SetFloat(PROP_CONDICION_PIXEL, PixeladoActivo ? 100f : 0f);
        _prevPixeladoActivo = PixeladoActivo;
    }

    private void SetFloat(string property, float value)
    {
        estadoDeVidaMaterial.SetFloat(property, value);
    }
}

