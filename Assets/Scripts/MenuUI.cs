using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuUI : MonoBehaviour
{
    [SerializeField] int numCenaJogar = 1;
    [SerializeField] string nomeCenaJogar = "JogoV2";
    [SerializeField] GameObject painelCreditos;
    [SerializeField] GameObject painelMenu;
    bool boolPainelCreditos = false;
    bool boolPainelMenu = true;

    void Start()
    {
        
    }

    void Update()
    {
        
    }

    public void Jogar()
    {
        if (!string.IsNullOrEmpty(nomeCenaJogar))
        {
            SceneManager.LoadScene(nomeCenaJogar);
        }
        else
        {
            SceneManager.LoadScene(numCenaJogar);
        }
    }

    public void Sair()
    {
        Application.Quit();
    }
    public void PainelCreditos()
    {
        boolPainelCreditos = !boolPainelCreditos;
        painelCreditos.SetActive(boolPainelCreditos);
        boolPainelMenu = !boolPainelMenu;
        painelMenu.SetActive(boolPainelMenu);
    }
}
