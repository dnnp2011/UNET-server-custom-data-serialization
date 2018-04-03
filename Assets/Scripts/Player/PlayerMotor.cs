using UnityEngine;

public class PlayerMotor : MonoBehaviour
{
	public float MoveSpeed;

	private CharacterController controller;
	private Vector3 totalVelocity;
	private float xMov;
	private float zMov;
	private float yMov;

	void Start()
	{
		controller = GetComponent<CharacterController>();
		totalVelocity = Vector3.zero;
		xMov = 0f;
		yMov = 0f;
		zMov = 0f;
	}

	void Update()
	{
		GetInput();
	}

	void FixedUpdate()
	{
		ApplyMovement();
		
	}

	void GetInput()
	{
		xMov = Input.GetAxis("Horizontal");
		zMov = Input.GetAxis("Vertical");

		if(controller.isGrounded)
		{
			yMov = -1;
			if(Input.GetButton("Jump"))
			{
				yMov = 8f;
			}
		}
		else
		{
			yMov -= 14f * Time.deltaTime;
		}



		totalVelocity = new Vector3(xMov, 0f, zMov).normalized * MoveSpeed;
		totalVelocity.y = yMov;
	}

	void ApplyMovement()
	{
		controller.Move(totalVelocity * Time.fixedDeltaTime);
	}
}