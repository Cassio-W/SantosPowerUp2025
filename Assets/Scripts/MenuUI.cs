using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuUI : MonoBehaviour
{
    [SerializeField] int numCenaJogar;
    [SerializeField] GameObject painelSair;
    [SerializeField] GameObject painelCreditos;
    bool boolPainelSair = false;
    bool boolPainelCreditos = false;

    void Start()
    {
        
    }

    void Update()
    {
        
    }

    public void Jogar()
    {
        SceneManager.LoadScene(numCenaJogar);
    }

    public void PainelSair()
    {
        boolPainelSair = !boolPainelSair;
        painelSair.SetActive(boolPainelSair);
    }

    public void Sair()
    {
        Application.Quit();
    }
    public void PainelCreditos()
    {
        boolPainelCreditos = !boolPainelCreditos;
        painelCreditos.SetActive(boolPainelCreditos);
    }
}
