using System;
using System.Collections;
using ProjetoCarta.Core;
using ProjetoCarta.Interaction;
using ProjetoCarta.Inventory;
using ProjetoCarta.NoirUI;
using UnityEngine;
using UnityEngine.Events;

namespace ProjetoCarta.Puzzles
{
    /// <summary>
    /// Poco da Ultima Noite (P2). Fluxo:
    /// 1) prender o balde na corda e encaixar a manivela no guincho (em qualquer ordem). Basta mirar em qualquer
    ///    parte do poco (o volume <see cref="InteractionVolume"/> cobre a estrutura); a peca vai sempre para o seu
    ///    ponto (gancho ou ponta do eixo). <see cref="WellPart"/> continua aceitando o clique direto no ponto;
    /// 2) colocar uma isca no balde (pasta ou ima);
    /// 3) girar a manivela: manivela, tambor, corda e balde andam juntos ate o fundo e voltam;
    /// 4) o resultado sobe no balde: o clipe da pasta prende o envelope e o ima puxa o relogio. As duas iscas
    ///    ficam no fundo do poco (o ima e consumido: nao volta ao inventario).
    /// Cada isca funciona uma unica vez. Toda a movimentacao e feita aqui (sem Animator), para que
    /// manivela, corda, balde e conteudo andem sempre juntos.
    /// </summary>
    public sealed class WellController : RaycastInteractable
    {
        [Serializable]
        public sealed class BaitRecipe
        {
            [Tooltip("Item colocado no balde.")]
            public InventoryItemDefinition bait;
            [Tooltip("Pickup que sobe no lugar da isca (fica inativo ate o balde voltar).")]
            public InventoryPickup result;
            [Tooltip("Pose local da isca dentro do balde (relativa ao ponto de conteudo).")]
            public Vector3 baitLocalPosition;
            public Vector3 baitLocalEuler;
            [Tooltip("Tamanho aproximado (maior lado, em metros) da isca dentro do balde.")]
            public float baitWorldSize = 0.22f;
            public Vector3 resultLocalPosition;
            public Vector3 resultLocalEuler;
            public float resultWorldSize = 0.2f;
            [Tooltip("A isca fica no fundo do poco quando o resultado sobe (a pasta: o clipe prende o envelope e ela fica la).")]
            public bool baitStaysAtBottom;
            [Tooltip("A isca sobe com o resultado preso nela e volta ao inventario quando o resultado e pego (o ima).")]
            public bool baitReturnsWithResult;
            [TextArea(1, 2)] public string placedThought;
            [Tooltip("Dito quando o balde chega com o resultado: explica como ele foi recuperado.")]
            [TextArea(1, 2)] public string resultThought;
            [Tooltip("Dito quando a isca volta ao inventario (baitReturnsWithResult).")]
            [TextArea(1, 2)] public string baitReturnedThought;

            [NonSerialized] public bool used;
        }

        /// <summary>Pontos do poco com interacao propria (ver <see cref="WellPart"/>).</summary>
        public enum PartRole
        {
            BucketHook,
            CrankSocket
        }

        [Header("Balde")]
        [SerializeField] private InventoryItemDefinition bucketItem;
        [Tooltip("Ponto que sobe e desce. O balde e pendurado aqui.")]
        [SerializeField] private Transform bucketHook;
        [Tooltip("Onde a isca/resultado fica dentro do balde (filho do gancho).")]
        [SerializeField] private Transform contentAnchor;
        [Tooltip("Tamanho (maior lado, em metros) do balde pendurado.")]
        [SerializeField, Min(0.05f)] private float bucketWorldSize = 0.38f;
        [SerializeField] private Vector3 bucketLocalEuler;
        [Tooltip("Deslocamento local do balde em relacao ao gancho (alca no gancho).")]
        [SerializeField] private Vector3 bucketLocalOffset = new(0f, -0.2f, 0f);

        [Header("Corda e tambor")]
        [SerializeField] private WellRope rope;
        [Tooltip("Tambor do guincho (gira com a manivela).")]
        [SerializeField] private Transform crank;
        [SerializeField] private Vector3 crankAxis = Vector3.right;
        [SerializeField] private float crankDegreesPerMeter = 360f;

        [Header("Manivela")]
        [Tooltip("Item que faz o guincho girar. Vazio = o guincho funciona sem manivela.")]
        [SerializeField] private InventoryItemDefinition crankItem;
        [Tooltip("Ponta do eixo onde a manivela e encaixada. Filho do tambor: a manivela gira com ele.")]
        [SerializeField] private Transform crankSocket;
        [SerializeField] private Vector3 crankHandleLocalPosition;
        [SerializeField] private Vector3 crankHandleLocalEuler;
        [Tooltip("Tamanho (maior lado, em metros) da manivela encaixada.")]
        [SerializeField, Min(0.05f)] private float crankHandleWorldSize = 0.3f;

        [Header("Percurso")]
        [Tooltip("Quanto o gancho desce (metros) a partir da posicao de repouso.")]
        [SerializeField, Min(0.5f)] private float dropDistance = 3.2f;
        [SerializeField, Min(0.2f)] private float descendDuration = 2.4f;
        [SerializeField, Min(0f)] private float bottomWait = 1.4f;
        [SerializeField, Min(0.2f)] private float ascendDuration = 2.8f;
        [Tooltip("Onde fica a isca que nao sobe (fundo do poco).")]
        [SerializeField] private Transform bottomAnchor;

        [Header("Iscas")]
        [SerializeField] private BaitRecipe[] recipes = Array.Empty<BaitRecipe>();

        [Header("Prompts")]
        [SerializeField] private string idlePrompt = "Poço";
        [SerializeField] private string attachBucketPrompt = "Prender o balde na corda";
        [SerializeField] private string hookPrompt = "Gancho da corda";
        [SerializeField] private string emptyBucketPrompt = "Balde vazio";
        [SerializeField] private string placeBaitPrompt = "Colocar {0} no balde";
        [SerializeField] private string lowerPrompt = "Baixar o balde";
        [SerializeField] private string noCrankLowerPrompt = "Guincho sem manivela";
        [SerializeField] private string installCrankPrompt = "Encaixar a manivela no guincho";
        [SerializeField] private string crankSocketPrompt = "Eixo do guincho";

        [Header("Pensamentos")]
        [SerializeField, TextArea(1, 2)] private string noBucketThought = "Não consigo alcançar o fundo do poço com as mãos.";
        [SerializeField, TextArea(1, 2)] private string wrongItemNoBucketThought = "Isso não alcança o fundo. Preciso de algo preso à corda.";
        [SerializeField, TextArea(1, 2)] private string bucketOnWellThought = "O balde precisa ir no gancho da corda.";
        [SerializeField, TextArea(1, 2)] private string needBaitThought = "Preciso colocar alguma coisa no balde antes de baixá-lo.";
        [SerializeField, TextArea(1, 2)] private string wrongBaitThought = "Não vejo por que jogar isso no poço.";
        [SerializeField, TextArea(1, 2)] private string usedBaitThought = "O poço já me deu o que tinha para isso.";
        [SerializeField, TextArea(1, 2)] private string takeResultThought = "Primeiro vou pegar o que veio no balde.";
        [SerializeField, TextArea(1, 2)] private string exhaustedThought = "O fundo do poço já não tem mais nada para mim.";
        [SerializeField, TextArea(1, 2)] private string noCrankThought = "O guincho está sem manivela. Sem ela, o balde não desce.";
        [SerializeField, TextArea(1, 2)] private string crankOnWellThought = "A manivela encaixa na ponta do eixo do guincho.";
        [SerializeField, TextArea(1, 2)] private string socketEmptyThought = "Falta a manivela. Alguém a tirou do eixo do guincho.";
        [SerializeField, TextArea(1, 2)] private string socketWrongItemThought = "Isso não encaixa no eixo do guincho.";
        [SerializeField, TextArea(1, 2)] private string crankInstalledThought = "Encaixou. Agora o guincho gira.";

        [Header("Events")]
        [SerializeField] private UnityEvent onBucketAttached;
        [SerializeField] private UnityEvent onCrankInstalled;
        [SerializeField] private UnityEvent onCycleFinished;

        private enum WellState { NoBucket, Empty, Baited, Running, ResultReady }

        private WellState state = WellState.NoBucket;
        private GameObject bucketObject;
        private GameObject crankObject;
        private GameObject baitObject;
        private BaitRecipe currentRecipe;
        private bool resultSwapped;
        private bool crankInstalled;
        private InventoryController inventory;
        private Vector3 hookRestLocalPosition;
        private Quaternion crankCycleStart = Quaternion.identity;
        private Coroutine runRoutine;

        public bool HasBucket => state != WellState.NoBucket;
        public bool HasCrank => crankInstalled || crankItem == null;
        /// <summary>A manivela encaixada no guincho (null antes de instalar).</summary>
        public GameObject InstalledCrank => crankObject;
        public bool IsRunning => state == WellState.Running;

        private void Awake()
        {
            if (bucketHook != null)
                hookRestLocalPosition = bucketHook.localPosition;

            foreach (BaitRecipe recipe in recipes)
            {
                if (recipe?.result == null)
                    continue;

                recipe.result.gameObject.SetActive(false);
                recipe.result.PickedUp += OnResultPickedUp;
            }
        }

        private void OnDestroy()
        {
            foreach (BaitRecipe recipe in recipes)
            {
                if (recipe?.result != null)
                    recipe.result.PickedUp -= OnResultPickedUp;
            }
        }

        private void OnDisable()
        {
            // Piscar no meio da descida: conclui o ciclo imediatamente para nao deixar estado preso.
            if (runRoutine == null)
                return;

            StopCoroutine(runRoutine);
            runRoutine = null;
            CompleteCycleInstantly();
        }

        // ------------------------------------------------------------ interacao (corpo do poco)

        public override bool IsFocusable(InteractionContext context)
        {
            return base.IsFocusable(context) && state != WellState.Running;
        }

        public override bool CanInteract(InteractionContext context)
        {
            if (!IsFocusable(context))
                return false;

            // Balde ou manivela na mao: mirar em qualquer parte do poco prende/encaixa no ponto certo.
            if (CanInstallFromBody(context, out _))
                return true;

            return state switch
            {
                WellState.NoBucket => false,
                WellState.Empty => FindRecipe(context.EquippedItem, out BaitRecipe recipe) && !recipe.used,
                WellState.Baited => HasCrank,
                // Com o resultado no balde, clicar em qualquer parte do poco pega o resultado.
                WellState.ResultReady => currentRecipe?.result != null && currentRecipe.result.CanInteract(context),
                _ => false
            };
        }

        public override string GetPrompt(InteractionContext context)
        {
            if (CanInstallFromBody(context, out PartRole part))
                return GetPartPrompt(part, context);

            switch (state)
            {
                case WellState.Empty:
                    if (FindRecipe(context.EquippedItem, out BaitRecipe recipe) && !recipe.used)
                        return Loc.Format(placeBaitPrompt, Loc.T(recipe.bait.DisplayName).ToLowerInvariant());
                    return emptyBucketPrompt;
                case WellState.Baited:
                    return HasCrank ? lowerPrompt : noCrankLowerPrompt;
                case WellState.ResultReady:
                    return currentRecipe?.result != null ? currentRecipe.result.GetPrompt(context) : idlePrompt;
                default:
                    return idlePrompt;
            }
        }

        public override InteractionFeedback GetBlockedFeedback(InteractionContext context)
        {
            // Manivela na mao, ainda fora do guincho: aponta o lugar certo, em qualquer estado.
            if (!crankInstalled && crankItem != null && context.IsEquipped(crankItem) && state != WellState.ResultReady)
                return InteractionFeedback.Blocked(InteractionOutcome.WrongItem, crankOnWellThought);

            switch (state)
            {
                case WellState.NoBucket:
                    if (context.EquippedItem == null)
                        return InteractionFeedback.Blocked(InteractionOutcome.MissingItem, noBucketThought);
                    return context.IsEquipped(bucketItem)
                        ? InteractionFeedback.Blocked(InteractionOutcome.WrongItem, bucketOnWellThought)
                        : InteractionFeedback.Blocked(InteractionOutcome.WrongItem, wrongItemNoBucketThought);
                case WellState.Empty:
                    if (AllRecipesUsed())
                        return InteractionFeedback.Blocked(InteractionOutcome.NotPossible, exhaustedThought);
                    if (context.EquippedItem == null)
                        return InteractionFeedback.Blocked(InteractionOutcome.MissingItem, needBaitThought);
                    if (FindRecipe(context.EquippedItem, out BaitRecipe recipe) && recipe.used)
                        return InteractionFeedback.Blocked(InteractionOutcome.NotPossible, usedBaitThought);
                    return InteractionFeedback.Blocked(InteractionOutcome.WrongItem, wrongBaitThought);
                case WellState.Baited:
                    return InteractionFeedback.Blocked(InteractionOutcome.PrerequisiteMissing, noCrankThought);
                case WellState.ResultReady:
                    return InteractionFeedback.Blocked(InteractionOutcome.PrerequisiteMissing, takeResultThought);
                default:
                    return InteractionFeedback.None;
            }
        }

        public override void Interact(InteractionContext context)
        {
            if (!CanInteract(context))
                return;

            inventory = context.Inventory;
            if (CanInstallFromBody(context, out PartRole part))
            {
                UsePart(part, context);
                context.Interactor?.RefreshPrompt(this);
                return;
            }

            switch (state)
            {
                case WellState.Empty:
                    PlaceBait(context.Inventory);
                    break;
                case WellState.Baited:
                    runRoutine = StartCoroutine(RunCycle());
                    break;
                case WellState.ResultReady:
                    currentRecipe?.result?.Interact(context);
                    break;
            }

            context.Interactor?.RefreshPrompt(this);
        }

        // ------------------------------------------------------------ gancho e eixo (WellPart)

        public bool IsPartFocusable(PartRole role, InteractionContext context)
        {
            if (!base.IsFocusable(context))
                return false;

            return role switch
            {
                PartRole.BucketHook => state == WellState.NoBucket,
                PartRole.CrankSocket => crankItem != null && !crankInstalled,
                _ => false
            };
        }

        public bool CanUsePart(PartRole role, InteractionContext context)
        {
            if (!IsPartFocusable(role, context))
                return false;

            return role == PartRole.BucketHook ? context.IsEquipped(bucketItem) : context.IsEquipped(crankItem);
        }

        /// <summary>A peca na mao (manivela ou balde) que ainda falta no poco e pode ir para o seu ponto agora.</summary>
        private bool CanInstallFromBody(InteractionContext context, out PartRole part)
        {
            part = PartRole.CrankSocket;
            if (state == WellState.Running || context.EquippedItem == null)
                return false;

            if (crankSocket != null && CanUsePart(PartRole.CrankSocket, context))
                return true;

            part = PartRole.BucketHook;
            return bucketHook != null && CanUsePart(PartRole.BucketHook, context);
        }

        public string GetPartPrompt(PartRole role, InteractionContext context)
        {
            if (role == PartRole.BucketHook)
                return context.IsEquipped(bucketItem) ? attachBucketPrompt : hookPrompt;

            return context.IsEquipped(crankItem) ? installCrankPrompt : crankSocketPrompt;
        }

        public InteractionFeedback GetPartFeedback(PartRole role, InteractionContext context)
        {
            if (role == PartRole.CrankSocket)
            {
                return context.EquippedItem == null
                    ? InteractionFeedback.Blocked(InteractionOutcome.MissingItem, socketEmptyThought)
                    : InteractionFeedback.Blocked(InteractionOutcome.WrongItem, socketWrongItemThought);
            }

            if (context.EquippedItem == null)
                return InteractionFeedback.Blocked(InteractionOutcome.MissingItem, noBucketThought);
            if (!crankInstalled && crankItem != null && context.IsEquipped(crankItem))
                return InteractionFeedback.Blocked(InteractionOutcome.WrongItem, crankOnWellThought);
            return InteractionFeedback.Blocked(InteractionOutcome.WrongItem, wrongItemNoBucketThought);
        }

        public void UsePart(PartRole role, InteractionContext context)
        {
            if (!CanUsePart(role, context))
                return;

            inventory = context.Inventory;
            if (role == PartRole.BucketHook)
                AttachBucket(context.Inventory);
            else
                InstallCrank(context.Inventory);
        }

        // ------------------------------------------------------------ etapas

        private void AttachBucket(InventoryController owner)
        {
            if (!owner.TryRemoveAndGetObject(bucketItem, out GameObject bucket) || bucket == null)
                return;

            bucketObject = bucket;
            WorldItemPlacement.PrepareForWorld(bucket, bucketHook);
            Vector3 scale = WorldItemPlacement.ScaleToFit(bucket, bucketWorldSize, Quaternion.Euler(bucketLocalEuler), bucketHook);
            WorldItemPlacement.MoveTo(bucket.transform, bucketLocalOffset, Quaternion.Euler(bucketLocalEuler),
                scale, 0.55f, null, () => NoirAudio.Play(NoirSfx.BucketHook, 0.9f));

            state = WellState.Empty;
            onBucketAttached?.Invoke();
        }

        private void InstallCrank(InventoryController owner)
        {
            if (crankSocket == null || !owner.TryRemoveAndGetObject(crankItem, out GameObject handle) || handle == null)
                return;

            // A manivela passa a ser parte do guincho: sai do inventario e vira filha do tambor.
            crankObject = handle;
            crankInstalled = true;
            Quaternion rotation = Quaternion.Euler(crankHandleLocalEuler);
            WorldItemPlacement.PrepareForWorld(handle, crankSocket);
            Vector3 scale = WorldItemPlacement.ScaleToFit(handle, crankHandleWorldSize, rotation, crankSocket);
            WorldItemPlacement.MoveTo(handle.transform, crankHandleLocalPosition, rotation, scale, 0.5f, null,
                () => NoirAudio.Play(NoirSfx.Unlock, 0.7f, 0.8f));

            TomasThoughts.Say(crankInstalledThought);
            onCrankInstalled?.Invoke();
        }

        private void PlaceBait(InventoryController owner)
        {
            if (!FindRecipe(owner.EquippedItem, out BaitRecipe recipe) || recipe.used)
                return;

            if (!owner.TryRemoveAndGetObject(recipe.bait, out GameObject bait) || bait == null)
                return;

            currentRecipe = recipe;
            baitObject = bait;
            resultSwapped = false;
            Transform parent = contentAnchor != null ? contentAnchor : bucketHook;
            WorldItemPlacement.PrepareForWorld(bait, parent);
            Vector3 scale = WorldItemPlacement.ScaleToFit(bait, recipe.baitWorldSize, Quaternion.Euler(recipe.baitLocalEuler), parent);
            WorldItemPlacement.MoveTo(bait.transform, recipe.baitLocalPosition,
                Quaternion.Euler(recipe.baitLocalEuler), scale, 0.5f, null,
                () => NoirAudio.Play(NoirSfx.Place, 0.8f));

            TomasThoughts.Say(recipe.placedThought);
            state = WellState.Baited;
        }

        private IEnumerator RunCycle()
        {
            state = WellState.Running;
            if (crank != null)
                crankCycleStart = crank.localRotation;
            Vector3 top = hookRestLocalPosition;
            Vector3 bottom = top + Vector3.down * dropDistance;

            NoirAudio.Play(NoirSfx.Crank, 0.9f);
            yield return MoveHook(top, bottom, descendDuration, 1f);

            NoirAudio.Play(NoirSfx.Splash, 0.85f);
            SwapBaitForResult();
            yield return new WaitForSeconds(bottomWait);

            NoirAudio.Play(NoirSfx.Crank, 0.9f, 0.85f);
            yield return MoveHook(bottom, top, ascendDuration, -1f);

            NoirAudio.Play(NoirSfx.BucketHook, 0.7f);
            FinishCycle(true);
            runRoutine = null;
        }

        private IEnumerator MoveHook(Vector3 from, Vector3 to, float duration, float crankDirection)
        {
            float elapsed = 0f;
            Quaternion crankStart = crank != null ? crank.localRotation : Quaternion.identity;
            float distance = Vector3.Distance(from, to);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);
                bucketHook.localPosition = Vector3.Lerp(from, to, eased);

                // Leve balanco do balde enquanto anda.
                float sway = Mathf.Sin(elapsed * 5.3f) * 2.2f * (1f - Mathf.Abs(2f * t - 1f));
                bucketHook.localRotation = Quaternion.Euler(sway, 0f, sway * 0.6f);

                // O tambor (e a manivela encaixada nele) gira na mesma proporcao da corda que desenrola.
                if (crank != null)
                    crank.localRotation = crankStart *
                                          Quaternion.AngleAxis(eased * distance * crankDegreesPerMeter * crankDirection, crankAxis);

                rope?.Refresh();
                yield return null;
            }

            bucketHook.localPosition = to;
            bucketHook.localRotation = Quaternion.identity;
            if (crank != null)
                crank.localRotation = crankStart * Quaternion.AngleAxis(distance * crankDegreesPerMeter * crankDirection, crankAxis);
            rope?.Refresh();
        }

        private void SwapBaitForResult()
        {
            if (resultSwapped)
                return;

            resultSwapped = true;
            if (currentRecipe == null)
            {
                DiscardBait();
                return;
            }

            currentRecipe.used = true;
            if (currentRecipe.baitStaysAtBottom && bottomAnchor != null && baitObject != null)
            {
                // A pasta fica la embaixo; o clipe dela segue preso ao envelope que sobe.
                baitObject.transform.SetParent(bottomAnchor, true);
                baitObject.transform.localPosition = Vector3.zero;
                baitObject = null;
            }
            else if (!currentRecipe.baitReturnsWithResult)
            {
                DiscardBait();
            }

            InventoryPickup result = currentRecipe.result;
            if (result == null)
                return;

            Transform parent = contentAnchor != null ? contentAnchor : bucketHook;
            Transform resultTransform = result.transform;
            resultTransform.SetParent(parent, false);
            resultTransform.localPosition = currentRecipe.resultLocalPosition;
            resultTransform.localRotation = Quaternion.Euler(currentRecipe.resultLocalEuler);
            result.SetAvailable(false);
            // Bounds de renderer so existem com o objeto ativo.
            result.gameObject.SetActive(true);
            resultTransform.localScale = WorldItemPlacement.ScaleToFit(result.gameObject, currentRecipe.resultWorldSize,
                resultTransform.localRotation, parent);
        }

        private void DiscardBait()
        {
            if (baitObject == null)
                return;

            Destroy(baitObject);
            baitObject = null;
        }

        private void FinishCycle(bool withFeedback)
        {
            if (currentRecipe?.result != null)
            {
                currentRecipe.result.SetAvailable(true);
                state = WellState.ResultReady;
                if (withFeedback)
                    TomasThoughts.Say(currentRecipe.resultThought);
            }
            else
            {
                state = WellState.Empty;
            }

            onCycleFinished?.Invoke();
        }

        private void CompleteCycleInstantly()
        {
            if (state != WellState.Running)
                return;

            SwapBaitForResult();

            bucketHook.localPosition = hookRestLocalPosition;
            bucketHook.localRotation = Quaternion.identity;
            // Descida e subida se anulam: o tambor volta a pose de antes do ciclo.
            if (crank != null)
                crank.localRotation = crankCycleStart;
            rope?.Refresh();
            FinishCycle(false);
        }

        private void OnResultPickedUp(InventoryPickup pickup)
        {
            if (state != WellState.ResultReady || currentRecipe == null || currentRecipe.result != pickup)
                return;

            // O ima soltou o relogio: volta ao inventario, sem duplicar (TryAdd recusa um item repetido).
            if (currentRecipe.baitReturnsWithResult && baitObject != null)
            {
                InventoryController owner = inventory != null ? inventory : FindFirstObjectByType<InventoryController>();
                if (owner != null && owner.TryAdd(currentRecipe.bait, baitObject))
                    TomasThoughts.Say(currentRecipe.baitReturnedThought);
                else
                    DiscardBait();

                baitObject = null;
            }

            currentRecipe = null;
            state = WellState.Empty;
        }

        // ------------------------------------------------------------ utilitarios

        private bool FindRecipe(InventoryItemDefinition item, out BaitRecipe recipe)
        {
            recipe = null;
            if (item == null)
                return false;

            foreach (BaitRecipe candidate in recipes)
            {
                if (candidate?.bait == null)
                    continue;

                if (ReferenceEquals(candidate.bait, item) ||
                    string.Equals(candidate.bait.Id, item.Id, StringComparison.Ordinal))
                {
                    recipe = candidate;
                    return true;
                }
            }

            return false;
        }

        private bool AllRecipesUsed()
        {
            foreach (BaitRecipe recipe in recipes)
            {
                if (recipe != null && !recipe.used)
                    return false;
            }

            return true;
        }
    }
}
