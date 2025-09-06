using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuUI : MonoBehaviour
{
    [SerializeField] int numCenaJogar;
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
        SceneManager.LoadScene(numCenaJogar);
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
