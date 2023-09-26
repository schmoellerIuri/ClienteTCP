using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using PraticaSocketsCliente;

Console.Clear();

var ipEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 50000);

using Socket client = new(
    ipEndPoint.AddressFamily,
    SocketType.Stream,
    ProtocolType.Tcp);

await client.ConnectAsync(ipEndPoint);

byte[]? serverPublicKey = null;
var cryptoManager = new CryptoManager();

#region Troca de chaves para segredo compartilhado
try
{
    var sizeOfMessageBytes = new byte[4];
    _ = await client.ReceiveAsync(sizeOfMessageBytes, SocketFlags.None);

    int sizeOfMessage = BitConverter.ToInt32(sizeOfMessageBytes);

    serverPublicKey = new byte[sizeOfMessage];
    _ = await client.ReceiveAsync(serverPublicKey, SocketFlags.None);

    _ = await client.SendAsync(BitConverter.GetBytes(cryptoManager.PublicKey.Length), SocketFlags.None);
    _ = await client.SendAsync(cryptoManager.PublicKey, SocketFlags.None);
}
catch (SocketException)
{
    Console.WriteLine("Não foi possível conectar ao servidor.");
    return;
}
#endregion

#region Realização de autenticação
string authMessage = string.Empty;
try
{
    authMessage = await ReceiveDecryptedMessage(client, cryptoManager, serverPublicKey);

    var publicParams = cryptoManager.GetRSAParameters();

    SendEncryptedMessage(client, publicParams, cryptoManager, serverPublicKey);

    var signature = cryptoManager.Sign(Encoding.UTF8.GetBytes(authMessage));

    SendEncryptedBytes(client, signature, cryptoManager, serverPublicKey);

    var hashedAuthMessage = SHA256.HashData(Encoding.UTF8.GetBytes(authMessage));

    SendEncryptedBytes(client, hashedAuthMessage, cryptoManager, serverPublicKey);

    string response = await ReceiveDecryptedMessage(client, cryptoManager, serverPublicKey);

    Console.WriteLine($"Response: \"{response}\"");

    if (response.Contains("|<E>|"))
        throw new SocketException();

}
catch (SocketException)
{
    Console.WriteLine("Não foi possível conectar ao servidor, falha na autenticação.");
    return;
}

#endregion

while (true)
{
    Console.WriteLine("Selecione o tipo de operação que deseja fazer (C,R,U,D) - (Enter para sair):");
    var operacao = Console.ReadLine();

    Console.Clear();

    string request = string.Empty;

    if (!string.IsNullOrEmpty(operacao))
        operacao = operacao.ToUpper();

    switch (operacao)
    {
        case "C":
            request += "|<C>|";
            Console.WriteLine("Digite a descrição da sua tarefa:");
            request += string.Format("Descricao={0},", Console.ReadLine());
            request += string.Format("Status={0},", 0);
            Console.WriteLine("Digite a data de limite da sua tarefa(dd/MM/aaaa):");
            var data = Console.ReadLine();
            while (!DateTime.TryParse(data, out _))
            {
                Console.WriteLine("Data inválida, digite novamente:");
                data = Console.ReadLine();
            }
            request += string.Format("Data={0},", data);
            Console.WriteLine("Digite o nome do responsável pela tarefa:");
            request += string.Format("Responsavel={0}", Console.ReadLine());
            break;
        case "R":
            request += "|<R>|";
            Console.WriteLine("Digite o id da tarefa que deseja ler(0 para ler todas):");
            request += Console.ReadLine();
            break;
        case "U":
            request += "|<U>|";
            Console.WriteLine("Digite o id da tarefa que deseja atualizar:");
            request += string.Format("Id={0},", Console.ReadLine());
            Console.WriteLine("Digite a descrição da sua tarefa - (Digite enter se quiser manter o atributo) :");
            var descricao = Console.ReadLine();
            if (!string.IsNullOrEmpty(descricao))
                request += string.Format("Descricao={0},", descricao);
            Console.WriteLine("Qual o status da sua tarefa(0 - Pendente, 1 - Em andamento, 2 - Concluída) - (Digite enter se quiser manter o atributo):");
            var status = Console.ReadLine();
            if (!string.IsNullOrEmpty(status))
            {
                while (!int.TryParse(status, out _) || int.Parse(status) < 0 || int.Parse(status) > 2)
                {
                    Console.WriteLine("Status inválido, digite novamente:");
                    status = Console.ReadLine();
                }

                request += string.Format("Status={0},", status);
            }
            Console.WriteLine("Digite a data de limite da sua tarefa(dd/MM/aaaa) - (Digite enter se quiser manter o atributo):");
            var dataUpdate = Console.ReadLine();
            if (!string.IsNullOrEmpty(dataUpdate))
            {
                while (!DateTime.TryParse(dataUpdate, out _))
                {
                    Console.WriteLine("Data inválida, digite novamente:");
                    dataUpdate = Console.ReadLine();
                }

                request += string.Format("Data={0},", dataUpdate);
            }

            Console.WriteLine("Digite o nome do responsável pela tarefa - (Digite enter se quiser manter o atributo):");
            var responsavel = Console.ReadLine();
            if (!string.IsNullOrEmpty(responsavel))
                request += string.Format("Responsavel={0}", Console.ReadLine());
            break;
        case "D":
            request += "|<D>|";
            Console.WriteLine("Digite o id da tarefa que deseja deletar:");
            request += Console.ReadLine();
            break;
        case "":
            request += "|<E>|";
            break;
        default:
            request += "|<I>|";
            break;
    }

    Console.Clear();

    if (request == "|<E>|")
        break;

    #region Envio de IV + Requisição criptografada

    SendEncryptedMessage(client, request, cryptoManager, serverPublicKey);

    #endregion

    #region Recebimento de IV + Resposta criptografada

    string response = "";
    response = await ReceiveDecryptedMessage(client, cryptoManager, serverPublicKey);
    
    #endregion

    Console.WriteLine($"Response: \"{response}\"");
    Console.WriteLine("Pressione qualquer tecla para continuar...");
    Console.ReadKey();
    Console.Clear();
}

client.Close();

async void SendEncryptedMessage(Socket client, string message, CryptoManager cryptoManager, byte[] serverPublicKey)
{
    var encryptedRequest = cryptoManager.Encrypt(message, serverPublicKey);

    var sizeOfIVBytes = BitConverter.GetBytes(encryptedRequest.IV.Length);
    _ = await client.SendAsync(sizeOfIVBytes, SocketFlags.None);

    _ = await client.SendAsync(encryptedRequest.IV, SocketFlags.None);

    var sizeOfRequestBytes = BitConverter.GetBytes(encryptedRequest.encryptedMessage.Length);
    _ = await client.SendAsync(sizeOfRequestBytes, SocketFlags.None);

    _ = await client.SendAsync(encryptedRequest.encryptedMessage, SocketFlags.None);
}

async void SendEncryptedBytes(Socket client, byte[] message, CryptoManager cryptoManager, byte[] serverPublicKey)
{
    var encryptedRequest = cryptoManager.Encrypt(message, serverPublicKey);

    var sizeOfIVBytes = BitConverter.GetBytes(encryptedRequest.IV.Length);
    _ = await client.SendAsync(sizeOfIVBytes, SocketFlags.None);

    _ = await client.SendAsync(encryptedRequest.IV, SocketFlags.None);

    var sizeOfRequestBytes = BitConverter.GetBytes(encryptedRequest.encryptedMessage.Length);
    _ = await client.SendAsync(sizeOfRequestBytes, SocketFlags.None);

    _ = await client.SendAsync(encryptedRequest.encryptedMessage, SocketFlags.None);
}

async Task<string> ReceiveDecryptedMessage(Socket client, CryptoManager cryptoManager, byte[] serverPublicKey)
{
    var sizeOfIVResponseBytes = new byte[4];
    _ = await client.ReceiveAsync(sizeOfIVResponseBytes, SocketFlags.None);
    int sizeOfIVResponse = BitConverter.ToInt32(sizeOfIVResponseBytes);

    var ivResponse = new byte[sizeOfIVResponse];
    _ = await client.ReceiveAsync(ivResponse, SocketFlags.None);

    var sizeOfResponseBytes = new byte[4];
    _ = await client.ReceiveAsync(sizeOfResponseBytes, SocketFlags.None);
    int sizeOfResponse = BitConverter.ToInt32(sizeOfResponseBytes);

    var responseBytes = new byte[sizeOfResponse];
    _ = await client.ReceiveAsync(responseBytes, SocketFlags.None);
    string response = cryptoManager.Decrypt(responseBytes, ivResponse, serverPublicKey);

    return response;
}